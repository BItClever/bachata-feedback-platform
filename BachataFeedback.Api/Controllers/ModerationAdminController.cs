using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BachataFeedback.Api.Services.Moderation;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/admin/moderation")]
[Authorize(Roles = "Admin,Moderator")]
public class ModerationAdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IModerationQueue _queue;
    public ModerationAdminController(ApplicationDbContext db, IModerationQueue queue) { _db = db; _queue = queue; }

    public class UpdateModerationDto
    {
        public string Level { get; set; } = "Green"; // Green | Yellow | Red
        public string? Reason { get; set; }
        public string? ReasonRu { get; set; }
        public string? ReasonEn { get; set; }
    }

    public class RequeueDto
    {
        public string TargetType { get; set; } = string.Empty; // "Review" | "EventReview"
        public int TargetId { get; set; }
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs([FromQuery] string? status = null, [FromQuery] int take = 100)
    {
        var q = _db.ModerationJobs.AsNoTracking().OrderByDescending(x => x.CreatedAt);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status).OrderByDescending(x => x.CreatedAt);
        var items = await q.Take(take).ToListAsync();
        return Ok(items);
    }

    [HttpPost("requeue")]
    public async Task<IActionResult> Requeue([FromBody] RequeueDto body)
    {
        if (string.IsNullOrWhiteSpace(body.TargetType) || body.TargetId <= 0) return BadRequest();

        var tt = body.TargetType.Trim();
        if (tt != "Review" && tt != "EventReview") return BadRequest(new { success = false, message = "Invalid targetType" });

        _db.ModerationJobs.Add(new ModerationJob { TargetType = tt, TargetId = body.TargetId, Status = "Pending" });
        await _db.SaveChangesAsync();

        await _queue.EnqueueAsync(new ModerationMessage { TargetType = tt, TargetId = body.TargetId });
        return Ok(new { success = true });
    }

    // ---------- LIST of user reviews for moderation panel
    private static Dictionary<string, int>? SafeDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json); } catch { return null; }
    }

    [HttpGet("reviews/list")]
    public async Task<IActionResult> ListUserReviews(
        [FromQuery] string? status = "Pending", // Pending|Green|Yellow|Red|All
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var q = _db.Reviews
            .AsNoTracking()
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .OrderByDescending(r => r.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<ModerationLevel>(status, out var lvl))
                q = q.Where(r => r.ModerationLevel == lvl);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(r =>
                r.Reviewer.FirstName.ToLower().Contains(s) ||
                r.Reviewer.LastName.ToLower().Contains(s) ||
                r.Reviewee.FirstName.ToLower().Contains(s) ||
                r.Reviewee.LastName.ToLower().Contains(s) ||
                (r.Event != null && r.Event.Name.ToLower().Contains(s))
            );
        }

        var items = await q
            .Skip(skip)
            .Take(Math.Clamp(take, 1, 200))
            .Select(r => new
            {
                id = r.Id,
                type = "Review",
                createdAt = r.CreatedAt,
                reviewerId = r.ReviewerId,
                revieweeId = r.RevieweeId,
                reviewerName = r.IsAnonymous ? "Anonymous" : (r.Reviewer.FirstName + " " + r.Reviewer.LastName),
                revieweeName = r.Reviewee.FirstName + " " + r.Reviewee.LastName,
                eventId = r.EventId,
                eventName = r.Event != null ? r.Event.Name : null,
                text = r.TextReview,
                leadRatings = SafeDict(r.LeadRatings),
                followRatings = SafeDict(r.FollowRatings),

                moderationLevel = r.ModerationLevel.ToString(),
                moderationSource = r.ModerationSource.ToString(),
                moderatedAt = r.ModeratedAt,
                reason = r.ModerationReason,
                reasonRu = r.ModerationReasonRu,
                reasonEn = r.ModerationReasonEn,
                reportsCount = _db.Reports.Count(rep => rep.TargetType == "Review" && rep.TargetId == r.Id)
            })
            .ToListAsync();

        return Ok(items);
    }

    // ---------- LIST of event reviews for moderation panel
    [HttpGet("eventreviews/list")]
    public async Task<IActionResult> ListEventReviews(
        [FromQuery] string? status = "Pending",
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var q = _db.EventReviews
            .AsNoTracking()
            .Include(r => r.Event)
            .Include(r => r.Reviewer)
            .OrderByDescending(r => r.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<ModerationLevel>(status, out var lvl))
                q = q.Where(r => r.ModerationLevel == lvl);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(r =>
                r.Event.Name.ToLower().Contains(s) ||
                r.Reviewer.FirstName.ToLower().Contains(s) ||
                r.Reviewer.LastName.ToLower().Contains(s)
            );
        }

        var items = await q
            .Skip(skip)
            .Take(Math.Clamp(take, 1, 200))
            .Select(r => new
            {
                id = r.Id,
                type = "EventReview",
                createdAt = r.CreatedAt,
                reviewerId = r.ReviewerId,
                reviewerName = r.IsAnonymous ? "Anonymous" : (r.Reviewer.FirstName + " " + r.Reviewer.LastName),
                eventId = r.EventId,
                eventName = r.Event.Name,
                text = r.TextReview,
                ratings = SafeDict(r.Ratings),

                moderationLevel = r.ModerationLevel.ToString(),
                moderationSource = r.ModerationSource.ToString(),
                moderatedAt = r.ModeratedAt,
                reason = r.ModerationReason,
                reasonRu = r.ModerationReasonRu,
                reasonEn = r.ModerationReasonEn,
                reportsCount = _db.Reports.Count(rep => rep.TargetType == "EventReview" && rep.TargetId == r.Id)
            })
            .ToListAsync();

        return Ok(items);
    }

    // ---------- Reports by target
    [HttpGet("reports/by-target")]
    public async Task<IActionResult> GetReportsByTarget([FromQuery] string targetType, [FromQuery] int targetId, [FromQuery] string? status = null)
    {
        if (string.IsNullOrWhiteSpace(targetType) || targetId <= 0) return BadRequest();

        var q = _db.Reports.AsNoTracking().Where(r => r.TargetType == targetType && r.TargetId == targetId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);

        var items = await q
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.TargetType,
                r.TargetId,
                r.Reason,
                r.Description,
                r.Status,
                r.CreatedAt,
                reporterName = r.Reporter.FirstName + " " + r.Reporter.LastName
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPut("reviews/{id}")]
    public async Task<IActionResult> SetReviewLevel(int id, [FromBody] UpdateModerationDto dto)
    {
        var r = await _db.Reviews.FindAsync(id);
        if (r == null) return NotFound();

        if (!Enum.TryParse<ModerationLevel>(dto.Level, out var level)) return BadRequest("Invalid level");

        r.ModerationLevel = level;
        r.ModerationSource = ModerationSource.Manual;
        r.ModerationReason = dto.Reason;
        r.ModerationReasonRu = dto.ReasonRu ?? r.ModerationReasonRu;
        r.ModerationReasonEn = dto.ReasonEn ?? r.ModerationReasonEn;
        r.ModeratedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Reason) && string.IsNullOrWhiteSpace(dto.ReasonRu) && string.IsNullOrWhiteSpace(dto.ReasonEn))
        {
            r.ModerationReasonRu = dto.Reason;
            r.ModerationReasonEn = dto.Reason;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("eventreviews/{id}")]
    public async Task<IActionResult> SetEventReviewLevel(int id, [FromBody] UpdateModerationDto dto)
    {
        var r = await _db.EventReviews.FindAsync(id);
        if (r == null) return NotFound();

        if (!Enum.TryParse<ModerationLevel>(dto.Level, out var level)) return BadRequest("Invalid level");

        r.ModerationLevel = level;
        r.ModerationSource = ModerationSource.Manual;
        r.ModerationReason = dto.Reason;
        r.ModerationReasonRu = dto.ReasonRu ?? r.ModerationReasonRu;
        r.ModerationReasonEn = dto.ReasonEn ?? r.ModerationReasonEn;
        r.ModeratedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Reason) && string.IsNullOrWhiteSpace(dto.ReasonRu) && string.IsNullOrWhiteSpace(dto.ReasonEn))
        {
            r.ModerationReasonRu = dto.Reason;
            r.ModerationReasonEn = dto.Reason;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("photo-info/{photoId}")]
    public async Task<IActionResult> GetPhotoInfo(int photoId, [FromQuery] string? kind)
    {
        string BaseUrl(HttpRequest req) => $"{req.Scheme}://{req.Host}";
        var baseUrl = BaseUrl(Request);

        if (string.Equals(kind, "UserPhoto", StringComparison.OrdinalIgnoreCase))
        {
            var up = await _db.UserPhotos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == photoId);
            if (up == null) return NotFound(new { success = false, message = "Photo not found" });
            var userId = up.UserId;
            return Ok(new
            {
                kind = "User",
                userId,
                smallUrl = $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/small",
                mediumUrl = $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/medium",
                largeUrl = $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/large"
            });
        }

        if (string.Equals(kind, "EventPhoto", StringComparison.OrdinalIgnoreCase))
        {
            var ep = await _db.EventPhotos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == photoId);
            if (ep == null) return NotFound(new { success = false, message = "Photo not found" });
            var eventId = ep.EventId;
            return Ok(new
            {
                kind = "Event",
                eventId,
                smallUrl = $"{baseUrl}/api/files/events/{eventId}/photos/{photoId}/small",
                mediumUrl = $"{baseUrl}/api/files/events/{eventId}/photos/{photoId}/medium",
                largeUrl = $"{baseUrl}/api/files/events/{eventId}/photos/{photoId}/large"
            });
        }

        // Фолбэк: как было — пробуем user, затем event
        var upFallback = await _db.UserPhotos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == photoId);
        if (upFallback != null)
        {
            var userId = upFallback.UserId;
            return Ok(new
            {
                kind = "User",
                userId,
                smallUrl = $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/small",
                mediumUrl = $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/medium",
                largeUrl = $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/large"
            });
        }

        var epFallback = await _db.EventPhotos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == photoId);
        if (epFallback != null)
        {
            var eventId = epFallback.EventId;
            return Ok(new
            {
                kind = "Event",
                eventId,
                smallUrl = $"{baseUrl}/api/files/events/{eventId}/photos/{photoId}/small",
                mediumUrl = $"{baseUrl}/api/files/events/{eventId}/photos/{photoId}/medium",
                largeUrl = $"{baseUrl}/api/files/events/{eventId}/photos/{photoId}/large"
            });
        }

        return NotFound(new { success = false, message = "Photo not found" });
    }
}