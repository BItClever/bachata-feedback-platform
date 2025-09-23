using BachataFeedback.Api.Services.Storage;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IStorageService _storage;
    private readonly Data.ApplicationDbContext _db;

    public FilesController(IStorageService storage, Data.ApplicationDbContext db)
    {
        _storage = storage;
        _db = db;
    }

    private static string MapVariant(string basePath, string size)
    {
        // users/{userId}/{photoId}/original.jpg -> small/medium/large.jpg
        if (size.Equals("original", StringComparison.OrdinalIgnoreCase)) return basePath;
        return basePath.Replace("original.jpg", $"{size}.jpg");
    }

    [HttpGet("events/{eventId}/cover/{size}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEventCover(int eventId, string size, CancellationToken ct)
    {
        if (size is not ("small" or "medium" or "large" or "original"))
            return BadRequest(new { success = false, message = "Invalid size" });

        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev == null || string.IsNullOrEmpty(ev.CoverImagePath))
            return NotFound();

        var key = MapVariant(ev.CoverImagePath, size);
        if (!await _storage.ExistsAsync(key, ct)) return NotFound();

        var stream = await _storage.GetObjectAsync(key, ct);
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(stream, "image/jpeg");
    }

    [HttpGet("users/{userId}/photos/{photoId}/{size}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserPhoto(string userId, int photoId, string size, CancellationToken ct)
    {
        if (size is not ("small" or "medium" or "large" or "original"))
            return BadRequest(new { success = false, message = "Invalid size" });

        var photo = await _db.UserPhotos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == photoId && p.UserId == userId, ct);
        if (photo == null || string.IsNullOrEmpty(photo.FilePath))
            return NotFound();

        var key = MapVariant(photo.FilePath, size);
        if (!await _storage.ExistsAsync(key, ct)) return NotFound();

        var stream = await _storage.GetObjectAsync(key, ct);
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(stream, "image/jpeg");
    }

    [HttpGet("events/{eventId}/photos/{photoId}/{size}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEventPhoto(int eventId, int photoId, string size, CancellationToken ct)
    {
        if (size is not ("small" or "medium" or "large" or "original"))
            return BadRequest(new { success = false, message = "Invalid size" });

        var ph = await _db.EventPhotos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == photoId && p.EventId == eventId, ct);
        if (ph == null || string.IsNullOrEmpty(ph.FilePath))
            return NotFound();

        var key = MapVariant(ph.FilePath, size);
        if (!await _storage.ExistsAsync(key, ct)) return NotFound();

        var stream = await _storage.GetObjectAsync(key, ct);
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(stream, "image/jpeg");
    }
}