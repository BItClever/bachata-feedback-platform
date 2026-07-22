using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OccurrencesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public OccurrencesController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET /api/occurrences?type=lesson&status=published&date=2026-07-20
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] DateTime? date,
        [FromQuery] int? groupId,
        CancellationToken ct)
    {
        var query = _db.Occurrences
            .Include(o => o.DanceGroup)
            .Include(o => o.Attendances)
            .AsQueryable();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(o => o.Type == type);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Status == status);

        if (date.HasValue)
        {
            var d = date.Value.Date;
            query = query.Where(o => o.StartsAt >= d && o.StartsAt < d.AddDays(1));
        }

        if (groupId.HasValue)
            query = query.Where(o => o.DanceGroupId == groupId);

        var items = await query
            .OrderBy(o => o.StartsAt)
            .Take(100)
            .Select(o => new
            {
                o.Id,
                o.Type,
                o.Status,
                o.StartsAt,
                o.EndsAt,
                o.Title,
                o.Level,
                o.Capacity,
                o.BalanceMales,
                o.BalanceFemales,
                o.Notes,
                o.CreatedAt,
                DanceGroup = o.DanceGroup == null ? null : new { o.DanceGroup.Id, o.DanceGroup.Name },
                AttendanceCount = o.Attendances.Count(a => a.Status == AttendanceStatus.Going),
                NotGoingCount = o.Attendances.Count(a => a.Status == AttendanceStatus.NotGoing)
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = items });
    }

    // GET /api/occurrences/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var o = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .Include(o => o.Attendances)
            .Include(o => o.Publications)
                .ThenInclude(p => p.TelegramChat)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (o == null) return NotFound(new { success = false, message = "Occurrence not found" });

        var result = new
        {
            o.Id,
            o.Type,
            o.Status,
            o.StartsAt,
            o.EndsAt,
            o.Title,
            o.Level,
            o.Capacity,
            o.BalanceMales,
            o.BalanceFemales,
            o.Notes,
            o.CreatedAt,
            DanceGroup = o.DanceGroup == null ? null : new { o.DanceGroup.Id, o.DanceGroup.Name },
            Attendance = new
            {
                Going = o.Attendances.Count(a => a.Status == AttendanceStatus.Going),
                NotGoing = o.Attendances.Count(a => a.Status == AttendanceStatus.NotGoing),
                Males = o.Attendances.Count(a => a.Status == AttendanceStatus.Going && a.DancerRole == DancerRoleAttendance.Male),
                Females = o.Attendances.Count(a => a.Status == AttendanceStatus.Going && a.DancerRole == DancerRoleAttendance.Female),
                Trainers = o.Attendances.Count(a => a.Status == AttendanceStatus.Going && a.DancerRole == DancerRoleAttendance.Trainer),
                List = o.Attendances
                    .Where(a => a.Status == AttendanceStatus.Going)
                    .Select(a => new
                    {
                        a.Id,
                        a.TelegramUserId,
                        a.TelegramUsername,
                        a.TelegramDisplayName,
                        a.UserId,
                        a.Status,
                        a.Source,
                        a.DancerRole,
                        a.UpdatedAt
                    })
            },
            Publications = o.Publications.Select(p => new
            {
                p.Id,
                p.TelegramChatId,
                ChatTitle = p.TelegramChat.Title,
                p.PublicationType,
                p.IsVotingSource,
                p.TelegramPollId,
                p.TelegramMessageId,
                p.PublishedAt
            })
        };

        return Ok(new { success = true, data = result });
    }

    // POST /api/occurrences
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateOccurrenceRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Invalid request" });

        var occurrence = new Occurrence
        {
            Type = req.Type,
            DanceGroupId = req.DanceGroupId,
            StartsAt = DateTime.SpecifyKind(req.StartsAt, DateTimeKind.Utc),
            EndsAt = req.EndsAt.HasValue ? DateTime.SpecifyKind(req.EndsAt.Value, DateTimeKind.Utc) : null,
            Title = req.Title,
            Level = req.Level,
            Capacity = req.Capacity,
            BalanceMales = req.BalanceMales,
            BalanceFemales = req.BalanceFemales,
            Notes = req.Notes,
            Status = OccurrenceStatus.Draft
        };

        _db.Occurrences.Add(occurrence);
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, data = new { occurrence.Id } });
    }

    // PUT /api/occurrences/{id}
    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOccurrenceRequest req, CancellationToken ct)
    {
        var occurrence = await _db.Occurrences.FindAsync(new object[] { id }, ct);
        if (occurrence == null) return NotFound(new { success = false, message = "Occurrence not found" });

        if (req.Title != null) occurrence.Title = req.Title;
        if (req.Level != null) occurrence.Level = req.Level;
        if (req.Notes != null) occurrence.Notes = req.Notes;
        if (req.Capacity.HasValue) occurrence.Capacity = req.Capacity;
        if (req.BalanceMales.HasValue) occurrence.BalanceMales = req.BalanceMales;
        if (req.BalanceFemales.HasValue) occurrence.BalanceFemales = req.BalanceFemales;
        if (req.StartsAt.HasValue)
            occurrence.StartsAt = DateTime.SpecifyKind(req.StartsAt.Value, DateTimeKind.Utc);
        if (req.EndsAt.HasValue)
            occurrence.EndsAt = DateTime.SpecifyKind(req.EndsAt.Value, DateTimeKind.Utc);
        if (req.DanceGroupId.HasValue) occurrence.DanceGroupId = req.DanceGroupId;

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    // POST /api/occurrences/{id}/cancel
    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var occurrence = await _db.Occurrences.FindAsync(new object[] { id }, ct);
        if (occurrence == null) return NotFound(new { success = false, message = "Occurrence not found" });

        occurrence.Status = OccurrenceStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true });
    }

    // GET /api/occurrences/groups — список танцевальных групп
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups(CancellationToken ct)
    {
        var groups = await _db.DanceGroups
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .Select(g => new { g.Id, g.Name, g.Description })
            .ToListAsync(ct);

        return Ok(new { success = true, data = groups });
    }

    // POST /api/occurrences/groups
    [HttpPost("groups")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateDanceGroupRequest req, CancellationToken ct)
    {
        var group = new DanceGroup
        {
            Name = req.Name,
            Description = req.Description,
            SortOrder = req.SortOrder
        };

        _db.DanceGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, data = new { group.Id } });
    }

    // POST /api/occurrences/{id}/attendance — ручное добавление записи (admin)
    [HttpPost("{id:int}/attendance")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AddAttendance(int id, [FromBody] AddAttendanceRequest req, CancellationToken ct)
    {
        var occurrence = await _db.Occurrences.FindAsync(new object[] { id }, ct);
        if (occurrence == null) return NotFound(new { success = false, message = "Occurrence not found" });

        // Дедупликация по UserId или TelegramUserId
        Attendance? existing = null;
        if (req.UserId != null)
            existing = await _db.Attendances.FirstOrDefaultAsync(
                a => a.OccurrenceId == id && a.UserId == req.UserId, ct);
        else if (req.TelegramUserId.HasValue)
            existing = await _db.Attendances.FirstOrDefaultAsync(
                a => a.OccurrenceId == id && a.TelegramUserId == req.TelegramUserId, ct);

        if (existing != null)
        {
            existing.Status = req.Status ?? AttendanceStatus.Going;
            existing.DancerRole = req.DancerRole ?? existing.DancerRole;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Attendances.Add(new Attendance
            {
                OccurrenceId = id,
                UserId = req.UserId,
                TelegramUserId = req.TelegramUserId,
                TelegramUsername = req.TelegramUsername,
                TelegramDisplayName = req.DisplayName,
                Status = req.Status ?? AttendanceStatus.Going,
                Source = AttendanceSource.AdminManual,
                DancerRole = req.DancerRole,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

public record CreateOccurrenceRequest(
    string Type,
    DateTime StartsAt,
    DateTime? EndsAt,
    int? DanceGroupId,
    string? Title,
    string? Level,
    int? Capacity,
    int? BalanceMales,
    int? BalanceFemales,
    string? Notes);

public record UpdateOccurrenceRequest(
    DateTime? StartsAt,
    DateTime? EndsAt,
    int? DanceGroupId,
    string? Title,
    string? Level,
    int? Capacity,
    int? BalanceMales,
    int? BalanceFemales,
    string? Notes);

public record CreateDanceGroupRequest(string Name, string? Description, int SortOrder = 0);

public record AddAttendanceRequest(
    string? UserId,
    long? TelegramUserId,
    string? TelegramUsername,
    string? DisplayName,
    string? Status,
    string? DancerRole);
