using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public ReportsController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public class CreateReportDto
    {
        public string TargetType { get; set; } = string.Empty; // "Review" | "Photo" | "EventReview"
        public int TargetId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportDto dto)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var allowed = new[] { "Review", "EventReview", "Photo", "UserPhoto", "EventPhoto" };
        if (!allowed.Contains(dto.TargetType))
            return BadRequest(new { success = false, message = "Invalid target type" });

        var report = new Report
        {
            ReporterId = currentUser.Id,
            TargetType = dto.TargetType,
            TargetId = dto.TargetId,
            Reason = dto.Reason,
            Description = dto.Description,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "Report submitted" });
    }
}