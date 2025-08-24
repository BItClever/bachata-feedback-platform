using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserSettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public UserSettingsController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public class UpdateSettingsDto
    {
        public bool AllowReviews { get; set; } = true;
        public bool ShowRatingsToOthers { get; set; } = true;
        public bool ShowTextReviewsToOthers { get; set; } = true;
        public bool AllowAnonymousReviews { get; set; } = true;
        public bool ShowPhotosToGuests { get; set; } = true;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMySettings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var s = await _context.UserSettings.FindAsync(user.Id) ?? new UserSettings { UserId = user.Id };
        return Ok(new UpdateSettingsDto
        {
            AllowReviews = s.AllowReviews,
            ShowRatingsToOthers = s.ShowRatingsToOthers,
            ShowTextReviewsToOthers = s.ShowTextReviewsToOthers,
            AllowAnonymousReviews = s.AllowAnonymousReviews,
            ShowPhotosToGuests = s.ShowPhotosToGuests
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMySettings([FromBody] UpdateSettingsDto dto)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var s = await _context.UserSettings.FindAsync(user.Id);
        if (s == null)
        {
            s = new UserSettings { UserId = user.Id };
            _context.UserSettings.Add(s);
        }

        s.AllowReviews = dto.AllowReviews;
        s.ShowRatingsToOthers = dto.ShowRatingsToOthers;
        s.ShowTextReviewsToOthers = dto.ShowTextReviewsToOthers;
        s.AllowAnonymousReviews = dto.AllowAnonymousReviews;
        s.ShowPhotosToGuests = dto.ShowPhotosToGuests;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Settings updated" });
    }
}