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
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var settings = await _context.UserSettings.FindAsync(currentUser.Id);
        if (settings == null)
        {
            settings = new UserSettings
            {
                UserId = currentUser.Id,
                AllowReviews = true,
                ShowRatingsToOthers = true,
                ShowTextReviewsToOthers = true,
                AllowAnonymousReviews = true,
                ShowPhotosToGuests = true
            };
            _context.UserSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            settings.AllowReviews,
            settings.ShowRatingsToOthers,
            settings.ShowTextReviewsToOthers,
            settings.AllowAnonymousReviews,
            settings.ShowPhotosToGuests
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMySettings([FromBody] UpdateSettingsDto dto)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var settings = await _context.UserSettings.FindAsync(currentUser.Id);
        if (settings == null)
        {
            // ленивое создание при первом обновлении
            settings = new UserSettings { UserId = currentUser.Id };
            _context.UserSettings.Add(settings);
        }

        settings.AllowReviews = dto.AllowReviews;
        settings.ShowRatingsToOthers = dto.ShowRatingsToOthers;
        settings.ShowTextReviewsToOthers = dto.ShowTextReviewsToOthers;
        settings.AllowAnonymousReviews = dto.AllowAnonymousReviews;
        settings.ShowPhotosToGuests = dto.ShowPhotosToGuests;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            settings.AllowReviews,
            settings.ShowRatingsToOthers,
            settings.ShowTextReviewsToOthers,
            settings.AllowAnonymousReviews,
            settings.ShowPhotosToGuests
        });
    }
}