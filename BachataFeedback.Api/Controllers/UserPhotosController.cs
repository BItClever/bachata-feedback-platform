using BachataFeedback.Api.DTOs;
using BachataFeedback.Api.Services.Antivirus;
using BachataFeedback.Api.Services.Images;
using BachataFeedback.Api.Services.Storage;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserPhotosController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly Data.ApplicationDbContext _db;
    private readonly IAntivirusScanner _av;
    private readonly IImageProcessor _img;
    private readonly IStorageService _storage;
    private readonly IConfiguration _cfg;
    public UserPhotosController(
    UserManager<User> userManager,
    Data.ApplicationDbContext db,
    IAntivirusScanner av,
    IImageProcessor img,
    IStorageService storage,
    IConfiguration cfg)
    {
        _userManager = userManager;
        _db = db;
        _av = av;
        _img = img;
        _storage = storage;
        _cfg = cfg;
    }

    private static async Task<string?> DetectImageMimeAsync(Stream s, CancellationToken ct)
    {
        // Читаем первые 12 байт и определяем по сигнатуре
        s.Position = 0;
        byte[] header = new byte[12];
        int read = await s.ReadAsync(header, 0, header.Length, ct);
        s.Position = 0;
        if (read < 3) return null;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return "image/jpeg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (read >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return "image/png";

        // WEBP: "RIFF"...."WEBP"
        if (read >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && // RIFF
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) // WEBP
            return "image/webp";

        return null;
    }

    [HttpPost("me/upload")]
    [RequestSizeLimit(20_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMyPhoto([FromForm] PhotoUploadDto form, CancellationToken ct)
    {
        var file = form.File;
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();

        var maxMb = _cfg.GetValue<int>("Uploads:MaxSizeMb");
        var allowed = _cfg.GetSection("Uploads:AllowedTypes").Get<string[]>() ?? Array.Empty<string>();

        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Empty file" });
        if (file.Length > maxMb * 1024L * 1024L)
            return BadRequest(new { success = false, message = $"File too large. Max {maxMb} MB" });

        await using var input = file.OpenReadStream();

        // Сигнатурная проверка + whitelist
        var detected = await DetectImageMimeAsync(input, ct);
        if (detected == null || !allowed.Contains(detected))
            return BadRequest(new { success = false, message = "Unsupported or invalid image format" });

        // Антивирус
        var clean = await _av.IsCleanAsync(input, ct);
        if (!clean)
            return BadRequest(new { success = false, message = "File failed antivirus scan" });
        input.Position = 0;

        // Обработка изображений (ресайз, очистка EXIF, перекод в jpeg)
        var variants = await _img.ProcessAsync(input, ct);

        // Сохраняем запись, чтобы получить photoId
        var photo = new UserPhoto
        {
            UserId = current.Id,
            IsMain = false,
            FilePath = ""
        };
        _db.UserPhotos.Add(photo);
        await _db.SaveChangesAsync(ct);

        var prefix = $"users/{current.Id}/{photo.Id}";
        foreach (var (name, (data, contentType)) in variants)
        {
            var key = $"{prefix}/{name}.jpg";
            await _storage.PutObjectAsync(key, data, contentType, ct);
        }

        photo.FilePath = $"{prefix}/original.jpg";
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            photoId = photo.Id,
            urls = new
            {
                small = $"/api/files/users/{current.Id}/photos/{photo.Id}/small",
                medium = $"/api/files/users/{current.Id}/photos/{photo.Id}/medium",
                large = $"/api/files/users/{current.Id}/photos/{photo.Id}/large"
            }
        });
    }

    public class SetMainDto { public int PhotoId { get; set; } }

    [HttpPost("me/set-main")]
    public async Task<IActionResult> SetMain([FromBody] SetMainDto dto, CancellationToken ct)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();

        var photo = await _db.UserPhotos.FirstOrDefaultAsync(p => p.Id == dto.PhotoId && p.UserId == current.Id, ct);
        if (photo == null) return NotFound(new { success = false, message = "Photo not found" });

        var mine = await _db.UserPhotos.Where(p => p.UserId == current.Id).ToListAsync(ct);
        foreach (var p in mine) p.IsMain = p.Id == photo.Id;
        await _db.SaveChangesAsync(ct);

        // Обновим MainPhotoPath у пользователя (для быстрой выборки в /auth/me)
        current.MainPhotoPath = photo.FilePath;
        await _userManager.UpdateAsync(current);

        return Ok(new { success = true });
    }

    [HttpDelete("me/{photoId}")]
    public async Task<IActionResult> Delete(int photoId, CancellationToken ct)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();

        var photo = await _db.UserPhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.UserId == current.Id, ct);
        if (photo == null) return NotFound(new { success = false, message = "Photo not found" });

        // Удаляем из хранилища все варианты
        var basePath = photo.FilePath; // users/{uid}/{photoId}/original.jpg
        var prefix = basePath.Replace("/original.jpg", "");
        foreach (var variant in new[] { "original", "small", "medium", "large" })
        {
            var key = $"{prefix}/{variant}.jpg";
            await _storage.DeleteObjectAsync(key, ct);
        }

        // Если удаляем главное фото — очистим указатель у пользователя
        if (photo.IsMain && current.MainPhotoPath == photo.FilePath)
        {
            current.MainPhotoPath = null;
            await _userManager.UpdateAsync(current);
        }

        _db.UserPhotos.Remove(photo);
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true });
    }

    // Список моих фото с готовыми URL для отображения на фронте
    [HttpGet("me")]
    public async Task<IActionResult> GetMyPhotos(CancellationToken ct)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();

        var photos = await _db.UserPhotos
            .Where(p => p.UserId == current.Id)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(ct);

        var items = photos.Select(p => new
        {
            id = p.Id,
            isMain = p.IsMain,
            smallUrl = $"/api/files/users/{current.Id}/photos/{p.Id}/small",
            mediumUrl = $"/api/files/users/{current.Id}/photos/{p.Id}/medium",
            largeUrl = $"/api/files/users/{current.Id}/photos/{p.Id}/large"
        });

        return Ok(items);
    }
}