using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/admin/moderation")]
[Authorize(Roles = "Admin,Moderator")]
public class ModerationAdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public ModerationAdminController(ApplicationDbContext db) => _db = db;

    public class UpdateModerationDto
    {
        public string Level { get; set; } = "Green"; // Green | Yellow | Red
        public string? Reason { get; set; }
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
    public async Task<IActionResult> Requeue([FromBody] dynamic body)
    {
        string targetType = body?.targetType;
        int targetId = body?.targetId;
        if (string.IsNullOrWhiteSpace(targetType) || targetId <= 0) return BadRequest();

        _db.ModerationJobs.Add(new ModerationJob { TargetType = targetType, TargetId = targetId, Status = "Pending" });
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
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
        r.ModeratedAt = DateTime.UtcNow;
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
        r.ModeratedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }
}