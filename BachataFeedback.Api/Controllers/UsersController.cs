using BachataFeedback.Api.Data;
using BachataFeedback.Api.DTOs;
using BachataFeedback.Api.Services;
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
        var baseUrl = BuildBaseUrl(Request);
        var ids = users.Select(u => u.Id).ToHashSet();

        var (grouped, focusMap) = await LoadRatingStatsAndFocus(ids);

        var result = users.Select(u =>
        {
            grouped.TryGetValue(u.Id, out var stat);
            return new
            {
                u.Id,
                // Email намеренно исключён из публичного списка (утечка PII)
                u.FirstName,
                u.LastName,
                u.Nickname,
                u.StartDancingDate,
                u.SelfAssessedLevel,
                u.Bio,
                u.DanceStyles,
                u.CreatedAt,
                u.DancerRole,
                mainPhotoSmallUrl = BuildPhotoUrl(baseUrl, u.Id, u.MainPhotoPath, "small"),
                mainPhotoMediumUrl = BuildPhotoUrl(baseUrl, u.Id, u.MainPhotoPath, "medium"),
                mainPhotoLargeUrl = BuildPhotoUrl(baseUrl, u.Id, u.MainPhotoPath, "large"),
                mainPhotoFocusX = focusMap.TryGetValue(u.Id, out var f) ? (float?)f.FocusX : null,
                mainPhotoFocusY = focusMap.TryGetValue(u.Id, out var f2) ? (float?)f2.FocusY : null,
                reviewsReceivedCount = stat?.count ?? 0,
                avgRating = stat?.avg,
                avgRatingUnique = stat?.avgUnique
            };
        });

        return Ok(result);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetUsersPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var q = _db.Users.AsNoTracking().Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(u =>
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s) ||
                (u.Nickname != null && u.Nickname.ToLower().Contains(s))
                // Email исключён из публичного поиска
            );
        }

        var total = await q.CountAsync();

        var pageUsers = await q
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                // Email намеренно исключён из публичного ответа (утечка PII)
                u.FirstName,
                u.LastName,
                u.Nickname,
                u.StartDancingDate,
                u.SelfAssessedLevel,
                u.Bio,
                u.DanceStyles,
                u.MainPhotoPath,
                u.CreatedAt,
                u.DancerRole
            })
            .ToListAsync();

        var baseUrl = BuildBaseUrl(Request);
        var ids = pageUsers.Select(u => u.Id).ToHashSet();
        var (grouped, focusMap) = await LoadRatingStatsAndFocus(ids);

        var items = pageUsers.Select(u =>
        {
            grouped.TryGetValue(u.Id, out var stat);
            return new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Nickname,
                u.StartDancingDate,
                u.SelfAssessedLevel,
                u.Bio,
                u.DanceStyles,
                u.CreatedAt,
                u.DancerRole,
                mainPhotoSmallUrl = BuildPhotoUrl(baseUrl, u.Id, u.MainPhotoPath, "small"),
                mainPhotoMediumUrl = BuildPhotoUrl(baseUrl, u.Id, u.MainPhotoPath, "medium"),
                mainPhotoLargeUrl = BuildPhotoUrl(baseUrl, u.Id, u.MainPhotoPath, "large"),
                mainPhotoFocusX = focusMap.TryGetValue(u.Id, out var f) ? (float?)f.FocusX : null,
                mainPhotoFocusY = focusMap.TryGetValue(u.Id, out var f2) ? (float?)f2.FocusY : null,
                reviewsReceivedCount = stat?.count ?? 0,
                avgRating = stat?.avg,
                avgRatingUnique = stat?.avgUnique
            };
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
    public async Task<ActionResult<UserProfileDto>> GetUser(string id)
    {
        var u = await _userService.GetUserByIdAsync(id);
        if (u == null)
            return NotFound(new { message = "User not found" });

        var baseUrl = BuildBaseUrl(Request);

        var mainPhotoFocus = await _db.UserPhotos
            .AsNoTracking()
            .Where(p => p.UserId == id && p.IsMain)
            .Select(p => new { p.FocusX, p.FocusY })
            .FirstOrDefaultAsync();

        var result = new
        {
            u.Id,
            // Email намеренно исключён из публичного профиля (утечка PII)
            u.FirstName,
            u.LastName,
            u.Nickname,
            u.StartDancingDate,
            u.SelfAssessedLevel,
            u.Bio,
            u.DanceStyles,
            u.CreatedAt,
            u.DancerRole,
            mainPhotoSmallUrl = BuildPhotoUrl(baseUrl, u.Id, u.MainPhotoPath, "small"),
            mainPhotoMediumUrl = BuildPhotoUrl(baseUrl, u.Id, u.MainPhotoPath, "medium"),
            mainPhotoLargeUrl = BuildPhotoUrl(baseUrl, u.Id, u.MainPhotoPath, "large"),
            mainPhotoFocusX = mainPhotoFocus?.FocusX,
            mainPhotoFocusY = mainPhotoFocus?.FocusY,
        };

        return Ok(result);
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

    // --- Private helpers ---

    private static string BuildBaseUrl(HttpRequest req) => $"{req.Scheme}://{req.Host}";

    private static string? BuildPhotoUrl(string baseUrl, string userId, string? mainPhotoPath, string size)
    {
        if (string.IsNullOrEmpty(mainPhotoPath)) return null;
        var parts = mainPhotoPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return null;
        if (!int.TryParse(parts[2], out var photoId)) return null;
        return $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/{size}";
    }

    /// <summary>
    /// Загружает агрегированные рейтинги и focus-координаты для набора пользователей.
    /// Выделено во избежание дублирования между GetUsers и GetUsersPaged.
    /// </summary>
    private async Task<(
        Dictionary<string, dynamic> grouped,
        Dictionary<string, dynamic> focusMap
    )> LoadRatingStatsAndFocus(HashSet<string> ids)
    {
        var reviews = await _db.Reviews
            .Where(r => ids.Contains(r.RevieweeId)
                     && r.ModerationLevel != ModerationLevel.Red
                     && r.ModerationLevel != ModerationLevel.Pending)
            .Select(r => new { r.RevieweeId, r.ReviewerId, r.LeadRatings, r.FollowRatings })
            .ToListAsync();

        var mainPhotoFocus = await _db.UserPhotos
            .AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && p.IsMain)
            .Select(p => new { p.UserId, p.FocusX, p.FocusY })
            .ToListAsync();

        var focusMap = mainPhotoFocus.ToDictionary(
            x => x.UserId,
            x => (dynamic)new { x.FocusX, x.FocusY });

        var grouped = reviews.GroupBy(r => r.RevieweeId).ToDictionary(
            g => g.Key,
            g =>
            {
                var perReview = g.Select(x =>
                {
                    var a = AvgDict(x.LeadRatings);
                    var b = AvgDict(x.FollowRatings);
                    var vals = new List<double>();
                    if (!double.IsNaN(a)) vals.Add(a);
                    if (!double.IsNaN(b)) vals.Add(b);
                    return vals.Count > 0 ? vals.Average() : double.NaN;
                }).ToArray();

                var starsOnly = perReview.Where(v => !double.IsNaN(v)).ToArray();
                var avg = starsOnly.Length > 0 ? starsOnly.Average() : double.NaN;

                var byAuthor = g.GroupBy(x => x.ReviewerId).Select(ga =>
                {
                    var authorVals = ga.Select(x =>
                    {
                        var a = AvgDict(x.LeadRatings);
                        var b = AvgDict(x.FollowRatings);
                        var vals = new List<double>();
                        if (!double.IsNaN(a)) vals.Add(a);
                        if (!double.IsNaN(b)) vals.Add(b);
                        return vals.Count > 0 ? vals.Average() : double.NaN;
                    }).Where(v => !double.IsNaN(v)).ToArray();
                    return authorVals.Length > 0 ? authorVals.Average() : double.NaN;
                }).Where(v => !double.IsNaN(v)).ToArray();

                var avgUnique = byAuthor.Length > 0 ? byAuthor.Average() : double.NaN;

                return (dynamic)new
                {
                    count = g.Count(),
                    avg = double.IsNaN(avg) ? (double?)null : Math.Round(avg, 2),
                    avgUnique = double.IsNaN(avgUnique) ? (double?)null : Math.Round(avgUnique, 2)
                };
            });

        return (grouped, focusMap);
    }

    private static double AvgDict(string? json)
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
}
