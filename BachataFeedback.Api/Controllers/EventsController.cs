using BachataFeedback.Api.Data;
using BachataFeedback.Api.DTOs;
using BachataFeedback.Api.Services.Antivirus;
using BachataFeedback.Api.Services.Images;
using BachataFeedback.Api.Services.Storage;
using BachataFeedback.Core.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IAntivirusScanner _av;
    private readonly IImageProcessor _img;
    private readonly IStorageService _storage;

    public EventsController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        IConfiguration configuration,
        IAntivirusScanner av,
        IImageProcessor img,
        IStorageService storage)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
        _av = av;
        _img = img;
        _storage = storage;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetEvents()
    {
        var currentUserId = _userManager.GetUserId(User);

        var data = await _context.Events
            .Include(e => e.Creator)
            .Include(e => e.Participants)
            .OrderByDescending(e => e.Date)
            .Select(e => new
            {
                e.Id,
                e.Name,
                e.Date,
                e.Location,
                e.Description,
                e.CreatedBy,
                CreatorFirst = e.Creator.FirstName,
                CreatorLast = e.Creator.LastName,
                e.CreatedAt,
                ParticipantCount = e.Participants.Count,
                IsParticipating = currentUserId != null && e.Participants.Any(p => p.UserId == currentUserId),
                e.CoverImagePath
            })
            .ToListAsync();

        string BaseUrl(HttpRequest req) => $"{req.Scheme}://{req.Host}";

        var list = data.Select(e => new EventDto
        {
            Id = e.Id,
            Name = e.Name,
            Date = e.Date,
            Location = e.Location,
            Description = e.Description,
            CreatedBy = e.CreatedBy,
            CreatorName = e.CreatorFirst + " " + e.CreatorLast,
            CreatedAt = e.CreatedAt,
            ParticipantCount = e.ParticipantCount,
            IsUserParticipating = e.IsParticipating,
            CoverImageSmallUrl = string.IsNullOrEmpty(e.CoverImagePath) ? null : $"{BaseUrl(Request)}/api/files/events/{e.Id}/cover/small",
            CoverImageLargeUrl = string.IsNullOrEmpty(e.CoverImagePath) ? null : $"{BaseUrl(Request)}/api/files/events/{e.Id}/cover/large"
        }).ToList();

        return Ok(list);
    }

    [HttpGet("paged")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEventsPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 12, [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 12;

        var currentUserId = _userManager.GetUserId(User);

        var q = _context.Events
            .AsNoTracking()
            .Include(e => e.Creator)
            .Include(e => e.Participants)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(e =>
                e.Name.ToLower().Contains(s) ||
                (e.Description != null && e.Description.ToLower().Contains(s)) ||
                (e.Location != null && e.Location.ToLower().Contains(s))
            );
        }

        var total = await q.CountAsync();

        var data = await q
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.Name,
                e.Date,
                e.Location,
                e.Description,
                e.CreatedBy,
                CreatorFirst = e.Creator.FirstName,
                CreatorLast = e.Creator.LastName,
                e.CreatedAt,
                ParticipantCount = e.Participants.Count,
                IsParticipating = currentUserId != null && e.Participants.Any(p => p.UserId == currentUserId),
                e.CoverImagePath
            })
            .ToListAsync();

        string BaseUrl(HttpRequest req) => $"{req.Scheme}://{req.Host}";
        var baseUrl = BaseUrl(Request);

        var items = data.Select(e => new EventDto
        {
            Id = e.Id,
            Name = e.Name,
            Date = e.Date,
            Location = e.Location,
            Description = e.Description,
            CreatedBy = e.CreatedBy,
            CreatorName = e.CreatorFirst + " " + e.CreatorLast,
            CreatedAt = e.CreatedAt,
            ParticipantCount = e.ParticipantCount,
            IsUserParticipating = e.IsParticipating,
            CoverImageSmallUrl = string.IsNullOrEmpty(e.CoverImagePath) ? null : $"{baseUrl}/api/files/events/{e.Id}/cover/small",
            CoverImageLargeUrl = string.IsNullOrEmpty(e.CoverImagePath) ? null : $"{baseUrl}/api/files/events/{e.Id}/cover/large"
        });

        return Ok(new
        {
            items,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventDto>> GetEvent(int id)
    {
        var currentUserId = _userManager.GetUserId(User);

        var e = await _context.Events
            .Include(ev => ev.Creator)
            .Include(ev => ev.Participants)
            .Where(ev => ev.Id == id)
            .Select(ev => new
            {
                ev.Id,
                ev.Name,
                ev.Date,
                ev.Location,
                ev.Description,
                ev.CreatedBy,
                CreatorFirst = ev.Creator.FirstName,
                CreatorLast = ev.Creator.LastName,
                ev.CreatedAt,
                ParticipantCount = ev.Participants.Count,
                IsParticipating = currentUserId != null && ev.Participants.Any(p => p.UserId == currentUserId),
                ev.CoverImagePath
            })
            .FirstOrDefaultAsync();

        if (e == null)
            return NotFound();

        string baseUrl = $"{Request.Scheme}://{Request.Host}";

        var dto = new EventDto
        {
            Id = e.Id,
            Name = e.Name,
            Date = e.Date,
            Location = e.Location,
            Description = e.Description,
            CreatedBy = e.CreatedBy,
            CreatorName = e.CreatorFirst + " " + e.CreatorLast,
            CreatedAt = e.CreatedAt,
            ParticipantCount = e.ParticipantCount,
            IsUserParticipating = e.IsParticipating,
            CoverImageSmallUrl = string.IsNullOrEmpty(e.CoverImagePath) ? null : $"{baseUrl}/api/files/events/{e.Id}/cover/small",
            CoverImageLargeUrl = string.IsNullOrEmpty(e.CoverImagePath) ? null : $"{baseUrl}/api/files/events/{e.Id}/cover/large"
        };

        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "events.create")]
    public async Task<ActionResult<EventDto>> CreateEvent([FromBody] CreateEventDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var eventEntity = new Event
        {
            Name = model.Name,
            Date = model.Date,
            Location = model.Location,
            Description = model.Description,
            CreatedBy = currentUser.Id
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Автоматически добавляем создателя как участника
        var participation = new EventParticipant
        {
            UserId = currentUser.Id,
            EventId = eventEntity.Id,
            IsConfirmed = true
        };

        _context.EventParticipants.Add(participation);
        await _context.SaveChangesAsync();

        var eventDto = new EventDto
        {
            Id = eventEntity.Id,
            Name = eventEntity.Name,
            Date = eventEntity.Date,
            Location = eventEntity.Location,
            Description = eventEntity.Description,
            CreatedBy = eventEntity.CreatedBy,
            CreatorName = currentUser.FirstName + " " + currentUser.LastName,
            CreatedAt = eventEntity.CreatedAt,
            ParticipantCount = 1,
            IsUserParticipating = true
        };

        return CreatedAtAction(nameof(GetEvent), new { id = eventEntity.Id }, eventDto);
    }

    [HttpPost("{id}/join")]
    [Authorize]
    public async Task<IActionResult> JoinEvent(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            return NotFound();

        var existingParticipation = await _context.EventParticipants
            .FirstOrDefaultAsync(ep => ep.EventId == id && ep.UserId == currentUser.Id);

        if (existingParticipation != null)
            return BadRequest(new { message = "Already participating in this event" });

        var participation = new EventParticipant
        {
            UserId = currentUser.Id,
            EventId = id,
            IsConfirmed = false
        };

        _context.EventParticipants.Add(participation);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Successfully joined the event" });
    }

    [HttpPost("{id}/leave")]
    [Authorize]
    public async Task<IActionResult> LeaveEvent(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var participation = await _context.EventParticipants
            .FirstOrDefaultAsync(ep => ep.EventId == id && ep.UserId == currentUser.Id);

        if (participation == null)
            return BadRequest(new { message = "Not participating in this event" });

        _context.EventParticipants.Remove(participation);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Successfully left the event" });
    }

    [HttpPost("{id}/cover")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCover(int id, [FromForm] PhotoUploadDto form, CancellationToken ct)
    {
        var file = form.File;
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (ev == null) return NotFound(new { success = false, message = "Event not found" });

        // Проверка прав: создатель или роли Admin/Moderator/Organizer
        var canEdit = ev.CreatedBy == currentUser.Id
            || User.IsInRole("Admin")
            || User.IsInRole("Moderator")
            || User.IsInRole("Organizer");
        if (!canEdit) return Forbid();

        var maxMb = _configuration.GetValue<int>("Uploads:MaxSizeMb");
        var allowed = _configuration.GetSection("Uploads:AllowedTypes").Get<string[]>() ?? Array.Empty<string>();

        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Empty file" });
        if (file.Length > maxMb * 1024L * 1024L)
            return BadRequest(new { success = false, message = $"File too large. Max {maxMb} MB" });

        await using var input = file.OpenReadStream();

        // Сигнатурная проверка + whitelist
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

        var detected = await DetectAsync(input, ct);
        if (detected == null || !allowed.Contains(detected))
            return BadRequest(new { success = false, message = "Unsupported or invalid image format" });

        // Антивирус
        var clean = await _av.IsCleanAsync(input, ct);
        if (!clean)
            return BadRequest(new { success = false, message = "File failed antivirus scan" });
        input.Position = 0;

        // Обработка (ресайз, очистка EXIF, перекод в jpeg)
        var variants = await _img.ProcessAsync(input, ct);

        var prefix = $"events/{ev.Id}/cover";
        foreach (var (name, (data, contentType)) in variants)
        {
            var key = $"{prefix}_{name}.jpg";
            await _storage.PutObjectAsync(key, data, contentType, ct);
        }

        ev.CoverImagePath = $"{prefix}_original.jpg";
        await _context.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            urls = new
            {
                small = $"/api/files/events/{ev.Id}/cover/small",
                large = $"/api/files/events/{ev.Id}/cover/large"
            }
        });
    }
}
