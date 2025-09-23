using BachataFeedback.Api.Data;
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
[Route("api/events/{eventId}/photos")]
[Authorize]
public class EventPhotosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<User> _um;
    private readonly IAntivirusScanner _av;
    private readonly IImageProcessor _img;
    private readonly IStorageService _storage;
    private readonly IConfiguration _cfg;

    public EventPhotosController(ApplicationDbContext db, UserManager<User> um, IAntivirusScanner av, IImageProcessor img, IStorageService storage, IConfiguration cfg)
    {
        _db = db; _um = um; _av = av; _img = img; _storage = storage; _cfg = cfg;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(int eventId, CancellationToken ct)
    {
        var exist = await _db.Events.AsNoTracking().AnyAsync(e => e.Id == eventId, ct);
        if (!exist) return NotFound();

        string BaseUrl(HttpRequest req) => $"{req.Scheme}://{req.Host}";
        var baseUrl = BaseUrl(Request);

        var items = await _db.EventPhotos.AsNoTracking()
            .Where(p => p.EventId == eventId)
            .OrderByDescending(p => p.UploadedAt)
            .Select(p => new
            {
                id = p.Id,
                smallUrl = $"{baseUrl}/api/files/events/{eventId}/photos/{p.Id}/small",
                mediumUrl = $"{baseUrl}/api/files/events/{eventId}/photos/{p.Id}/medium",
                largeUrl = $"{baseUrl}/api/files/events/{eventId}/photos/{p.Id}/large",
                uploadedAt = p.UploadedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(int eventId, [FromForm] MultiPhotoUploadDto form, CancellationToken ct)
    {
        var current = await _um.GetUserAsync(User);
        if (current == null) return Unauthorized();

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev == null) return NotFound(new { success = false, message = "Event not found" });

        // Разрешим загрузку участникам события и ролям Admin/Moderator/Organizer, а также создателю
        var isParticipant = await _db.EventParticipants.AnyAsync(ep => ep.EventId == eventId && ep.UserId == current.Id, ct);
        var canUpload = isParticipant || User.IsInRole("Admin") || User.IsInRole("Moderator") || User.IsInRole("Organizer") || ev.CreatedBy == current.Id;
        if (!canUpload) return Forbid();

        var files = form.Files ?? new List<IFormFile>();
        if (files.Count == 0) return BadRequest(new { success = false, message = "No files" });

        // Опциональные лимиты
        const int maxFiles = 20;
        if (files.Count > maxFiles) return BadRequest(new { success = false, message = $"Too many files (max {maxFiles})" });

        var maxMb = _cfg.GetValue<int>("Uploads:MaxSizeMb");
        var allowed = _cfg.GetSection("Uploads:AllowedTypes").Get<string[]>() ?? Array.Empty<string>();

        static async Task<string?> DetectAsync(Stream s, CancellationToken c)
        {
            s.Position = 0;
            byte[] header = new byte[12];
            int read = await s.ReadAsync(header, 0, header.Length, c);
            s.Position = 0;
            if (read < 3) return null;
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return "image/jpeg";
            if (read >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                return "image/png";
            if (read >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                return "image/webp";
            return null;
        }

        var results = new List<object>();

        foreach (var file in files)
        {
            if (file == null || file.Length == 0) continue;
            if (file.Length > maxMb * 1024L * 1024L)
                return BadRequest(new { success = false, message = $"File too large. Max {maxMb} MB" });

            await using var input = file.OpenReadStream();

            var detected = await DetectAsync(input, ct);
            if (detected == null || !allowed.Contains(detected))
                return BadRequest(new { success = false, message = "Unsupported or invalid image format" });

            // Антивирус
            var clean = await _av.IsCleanAsync(input, ct);
            if (!clean) return BadRequest(new { success = false, message = "File failed antivirus scan" });
            input.Position = 0;

            // Обработка изображений
            var variants = await _img.ProcessAsync(input, ct);

            // Создаём запись для photoId
            var photo = new EventPhoto
            {
                EventId = eventId,
                UploaderId = current.Id,
                FilePath = ""
            };
            _db.EventPhotos.Add(photo);
            await _db.SaveChangesAsync(ct);

            var prefix = $"events/{eventId}/photos/{photo.Id}";
            foreach (var (name, (data, contentType)) in variants)
            {
                var key = $"{prefix}/{name}.jpg";
                await _storage.PutObjectAsync(key, data, contentType, ct);
            }

            photo.FilePath = $"{prefix}/original.jpg";
            await _db.SaveChangesAsync(ct);

            string BaseUrl(HttpRequest req) => $"{req.Scheme}://{req.Host}";
            var baseUrl = BaseUrl(Request);

            results.Add(new
            {
                photoId = photo.Id,
                urls = new
                {
                    small = $"{baseUrl}/api/files/events/{eventId}/photos/{photo.Id}/small",
                    medium = $"{baseUrl}/api/files/events/{eventId}/photos/{photo.Id}/medium",
                    large = $"{baseUrl}/api/files/events/{eventId}/photos/{photo.Id}/large"
                }
            });
        }

        return Ok(new { success = true, uploaded = results });
    }

    [HttpDelete("{photoId}")]
    public async Task<IActionResult> Delete(int eventId, int photoId, CancellationToken ct)
    {
        var current = await _um.GetUserAsync(User);
        if (current == null) return Unauthorized();

        var photo = await _db.EventPhotos.Include(p => p.Event).FirstOrDefaultAsync(p => p.Id == photoId && p.EventId == eventId, ct);
        if (photo == null) return NotFound(new { success = false, message = "Photo not found" });

        // Автор удаления: загрузивший, создатель события, Admin/Moderator/Organizer
        var canDelete = photo.UploaderId == current.Id
            || photo.Event.CreatedBy == current.Id
            || User.IsInRole("Admin") || User.IsInRole("Moderator") || User.IsInRole("Organizer");

        if (!canDelete) return Forbid();

        var basePath = photo.FilePath; // events/{eventId}/photos/{photoId}/original.jpg
        var prefix = basePath.Replace("/original.jpg", "");
        foreach (var variant in new[] { "original", "small", "medium", "large" })
        {
            var key = $"{prefix}/{variant}.jpg";
            await _storage.DeleteObjectAsync(key, ct);
        }

        _db.EventPhotos.Remove(photo);
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true });
    }
}