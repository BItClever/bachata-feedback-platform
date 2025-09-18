using BachataFeedback.Api.Data;
using BachataFeedback.Api.Services;
using BachataFeedback.Core.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _db;
    public UsersController(IUserService userService, UserManager<User> userManager, ApplicationDbContext db)
    {
        _userService = userService;
        _userManager = userManager;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetUsers()
    {
        var users = await _userService.GetActiveUsersAsync();
        string BaseUrl(HttpRequest req) => $"{req.Scheme}://{req.Host}";
        var baseUrl = BaseUrl(Request);

        string? Build(string userId, string? mainPhotoPath, string size)
        {
            if (string.IsNullOrEmpty(mainPhotoPath)) return null;
            var parts = mainPhotoPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;
            var photoIdStr = parts[2];
            if (!int.TryParse(photoIdStr, out var photoId)) return null;
            return $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/{size}";
        }

        var ids = users.Select(u => u.Id).ToHashSet();
        var reviews = await _db.Reviews
            .Where(r => ids.Contains(r.RevieweeId) && r.ModerationLevel != ModerationLevel.Red)
            .Select(r => new { r.RevieweeId, r.ReviewerId, r.LeadRatings, r.FollowRatings })
            .ToListAsync();

        // хелпер: среднее по словарю
        static double AvgDict(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return double.NaN;
            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (dict == null || dict.Count == 0) return double.NaN;
                var vals = dict.Values.Where(v => v >= 1 && v <= 5).ToArray();
                if (vals.Length == 0) return double.NaN;
                return vals.Average();
            }
            catch { return double.NaN; }
        }

        var grouped = reviews.GroupBy(r => r.RevieweeId).ToDictionary(
            g => g.Key,
            g =>
            {
                // простой средний (только те отзывы, где есть хотя бы одна оценка)
                var perReview = g.Select(x =>
                {
                    var a = AvgDict(x.LeadRatings);
                    var b = AvgDict(x.FollowRatings);
                    var parts = new List<double>();
                    if (!double.IsNaN(a)) parts.Add(a);
                    if (!double.IsNaN(b)) parts.Add(b);
                    return parts.Count > 0 ? parts.Average() : double.NaN;
                }).ToArray();

                var starsOnly = perReview.Where(v => !double.IsNaN(v)).ToArray();
                var avg = starsOnly.Length > 0 ? starsOnly.Average() : double.NaN;

                // взвешенное «по авторам»
                var byAuthor = g.GroupBy(x => x.ReviewerId).Select(ga =>
                {
                    var authorReviews = ga.Select(x =>
                    {
                        var a = AvgDict(x.LeadRatings);
                        var b = AvgDict(x.FollowRatings);
                        var parts = new List<double>();
                        if (!double.IsNaN(a)) parts.Add(a);
                        if (!double.IsNaN(b)) parts.Add(b);
                        return parts.Count > 0 ? parts.Average() : double.NaN;
                    }).Where(v => !double.IsNaN(v)).ToArray();

                    return authorReviews.Length > 0 ? authorReviews.Average() : double.NaN;
                }).Where(v => !double.IsNaN(v)).ToArray();

                var avgUnique = byAuthor.Length > 0 ? byAuthor.Average() : double.NaN;
                return new
                {
                    count = g.Count(), // всего отзывов (включая текстовые)
                    avg = double.IsNaN(avg) ? (double?)null : Math.Round(avg, 2),
                    avgUnique = double.IsNaN(avgUnique) ? (double?)null : Math.Round(avgUnique, 2)
                };
            });

        var result = users.Select(u =>
        {
            grouped.TryGetValue(u.Id, out var stat);
            return new
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
                mainPhotoLargeUrl = Build(u.Id, u.MainPhotoPath, "large"),
                reviewsReceivedCount = stat?.count ?? 0,
                avgRating = stat?.avg,         // обычное среднее по звёздам
                avgRatingUnique = stat?.avgUnique // среднее по авторам
            };
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