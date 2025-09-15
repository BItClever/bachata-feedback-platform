using BachataFeedback.Api.Services;
using BachataFeedback.Core.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly UserManager<User> _userManager;

    public UsersController(IUserService userService, UserManager<User> userManager)
    {
        _userService = userService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetUsers()
    {
        var users = await _userService.GetActiveUsersAsync();

        string BaseUrl(HttpRequest req) => $"{req.Scheme}://{req.Host}";
        var baseUrl = BaseUrl(Request);

        string? Build(string userId, string? mainPhotoPath, string size)
        {
            // Ожидаем формат "users/{userId}/{photoId}/original.jpg"
            if (string.IsNullOrEmpty(mainPhotoPath)) return null;
            var parts = mainPhotoPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;
            var photoIdStr = parts[2];
            if (!int.TryParse(photoIdStr, out var photoId)) return null;
            return $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/{size}";
        }

        var result = users.Select(u => new
        {
            u.Id,
            u.Email,
            u.FirstName,
            u.LastName,
            u.Nickname,
            u.StartDancingDate,
            u.SelfAssessedLevel,
            u.Bio,
            u.DanceStyles,
            u.CreatedAt,
            u.DancerRole,

            mainPhotoSmallUrl = Build(u.Id, u.MainPhotoPath, "small"),
            mainPhotoMediumUrl = Build(u.Id, u.MainPhotoPath, "medium"),
            mainPhotoLargeUrl = Build(u.Id, u.MainPhotoPath, "large")
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileDto>> GetUser(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(user);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> GetCurrentUser()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var userProfile = await _userService.GetUserProfileAsync(currentUser.Id);
        return Ok(userProfile);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateProfileDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || currentUser.Id != id)
            return Forbid();

        var updated = await _userService.UpdateUserProfileAsync(id, model);
        if (updated == null)
            return NotFound(new { message = "User not found" });

        // Если добавили поле DancerRole в UserProfileDto — оно придет автоматически
        return Ok(updated);
    }
}