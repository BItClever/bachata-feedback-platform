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
            .Where(r => ids.Contains(r.RevieweeId) && r.ModerationLevel != ModerationLevel.Red && r.ModerationLevel != ModerationLevel.Pending)
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

    [HttpGet("paged")]
    public async Task<IActionResult> GetUsersPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Базовый запрос по активным пользователям с фильтром
        var q = _db.Users.AsNoTracking().Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(u =>
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s) ||
                (u.Nickname != null && u.Nickname.ToLower().Contains(s)) ||
                (u.Email != null && u.Email.ToLower().Contains(s))
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
                u.Email,
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

        // Построим URLы фото (как в /users)
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

        // Агрегаты по рейтингам — как в существующем /users
        var ids = pageUsers.Select(u => u.Id).ToHashSet();

        // Исключим Red из агрегатов (как в /users); Pending можно тоже исключить
        var reviews = await _db.Reviews
            .Where(r => ids.Contains(r.RevieweeId) && r.ModerationLevel != ModerationLevel.Red && r.ModerationLevel != ModerationLevel.Pending)
            .Select(r => new { r.RevieweeId, r.ReviewerId, r.LeadRatings, r.FollowRatings })
            .ToListAsync();

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
                // средняя по каждому отзыву (если есть lead/follow звёзды)
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

        var items = pageUsers.Select(u =>
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

        // Построим ссылки на фото (если есть MainPhotoPath)
        string BaseUrl(HttpRequest req) => $"{req.Scheme}://{req.Host}";
        var baseUrl = BaseUrl(Request);

        string? Build(string userId, string? mainPhotoPath, string size)
        {
            // "users/{userId}/{photoId}/original.jpg" -> /api/files/users/{userId}/photos/{photoId}/{size}
            if (string.IsNullOrEmpty(mainPhotoPath)) return null;
            var parts = mainPhotoPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;
            var photoIdStr = parts[2];
            if (!int.TryParse(photoIdStr, out var photoId)) return null;
            return $"{baseUrl}/api/files/users/{userId}/photos/{photoId}/{size}";
        }

        var result = new
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
}