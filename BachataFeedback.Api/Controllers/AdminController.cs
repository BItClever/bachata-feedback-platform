using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(ApplicationDbContext context, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var users = await _context.Users
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Nickname,
                u.IsActive,
                u.CreatedAt,
                ReviewsGivenCount = u.ReviewsGiven.Count,
                ReviewsReceivedCount = u.ReviewsReceived.Count
            })
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalUsers = await _context.Users.CountAsync();

        return Ok(new
        {
            users,
            totalUsers,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalUsers / pageSize)
        });
    }

    [HttpPut("users/{id}/toggle-active")]
    public async Task<IActionResult> ToggleUserActive(string id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new { message = $"User {(user.IsActive ? "activated" : "deactivated")} successfully" });
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> GetAllReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var reviews = await _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .Include(r => r.Reports)
            .Select(r => new
            {
                r.Id,
                ReviewerName = r.IsAnonymous ? "Anonymous" : $"{r.Reviewer.FirstName} {r.Reviewer.LastName}",
                RevieweeName = $"{r.Reviewee.FirstName} {r.Reviewee.LastName}",
                EventName = r.Event != null ? r.Event.Name : null,
                r.TextReview,
                r.IsAnonymous,
                r.CreatedAt,
                ReportsCount = r.Reports.Count
            })
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalReviews = await _context.Reviews.CountAsync();

        return Ok(new
        {
            reviews,
            totalReviews,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalReviews / pageSize)
        });
    }

    [HttpDelete("reviews/{id}")]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
            return NotFound();

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Review deleted successfully" });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports([FromQuery] string status = "Pending")
    {
        var reports = await _context.Reports
            .Include(r => r.Reporter)
            .Where(r => r.Status == status)
            .Select(r => new
            {
                r.Id,
                ReporterName = $"{r.Reporter.FirstName} {r.Reporter.LastName}",
                r.TargetType,
                r.TargetId,
                r.Reason,
                r.Description,
                r.Status,
                r.CreatedAt,
            })
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(reports);
    }

    [HttpPut("reports/{id}/resolve")]
    public async Task<IActionResult> ResolveReport(int id, [FromBody] ResolveReportDto model)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report == null)
            return NotFound();

        report.Status = "Resolved";
        report.ResolvedAt = DateTime.UtcNow;

        if (model.DeleteTarget)
        {
            if (report.TargetType == "Review")
            {
                var review = await _context.Reviews.FindAsync(report.TargetId);
                if (review != null)
                {
                    _context.Reviews.Remove(review);
                }
            }
            else if (report.TargetType == "EventReview")
            {
                var evReview = await _context.EventReviews.FindAsync(report.TargetId);
                if (evReview != null)
                {
                    _context.EventReviews.Remove(evReview);
                }
            }
            // TargetType "Photo" можно обработать аналогично при необходимости
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Report resolved successfully" });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
        var totalReviews = await _context.Reviews.CountAsync();
        var totalEvents = await _context.Events.CountAsync();
        var pendingReports = await _context.Reports.CountAsync(r => r.Status == "Pending");

        var recentActivity = await _context.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Select(r => new
            {
                Type = "Review",
                Message = $"{(r.IsAnonymous ? "Anonymous" : r.Reviewer.FirstName)} left a review for {r.Reviewee.FirstName}",
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            totalUsers,
            activeUsers,
            totalReviews,
            totalEvents,
            pendingReports,
            recentActivity
        });
    }

    [HttpPost("create-admin")]
    [AllowAnonymous] // Только для первоначальной настройки
    public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminDto model)
    {
        var adminRole = await _roleManager.FindByNameAsync("Admin");
        if (adminRole != null)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Any())
                return BadRequest(new { message = "Admin already exists" });
        }

        if (adminRole == null)
        {
            adminRole = new IdentityRole("Admin");
            await _roleManager.CreateAsync(adminRole);
        }

        var user = new User
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Admin");
            return Ok(new { message = "Admin created successfully" });
        }

        return BadRequest(result.Errors);
    }
}

public class ResolveReportDto
{
    public bool DeleteTarget { get; set; }
}

public class CreateAdminDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}