using BachataFeedback.Api.Data;
using BachataFeedback.Api.DTOs;
using BachataFeedback.Api.Services;
using BachataFeedback.Api.Services.Moderation;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly UserManager<User> _userManager;
    private readonly IModerationQueue _moderationQueue;
    private readonly ApplicationDbContext _db;

    public ReviewsController(IReviewService reviewService, UserManager<User> userManager, IModerationQueue moderationQueue, ApplicationDbContext db)
    {
        _reviewService = reviewService;
        _userManager = userManager;
        _moderationQueue = moderationQueue;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetReviews()
    {
        var requestorId = _userManager.GetUserId(User);
        var currentUserId = _userManager.GetUserId(User);
        bool isModerator = User.IsInRole("Admin") || User.IsInRole("Moderator");
        var reviews = await _reviewService.GetAllReviewsAsync(requestorId, isModerator);
        return Ok(reviews);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetUserReviews(string userId)
    {
        var requestorId = _userManager.GetUserId(User);
        var currentUserId = _userManager.GetUserId(User);

        var revieweeSettings = await _userManager.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Settings)
            .FirstOrDefaultAsync();

        bool isModerator = User.IsInRole("Admin") || User.IsInRole("Moderator");
        bool isOwner = currentUserId == userId;

        var reviews = await _reviewService.GetUserReviewsAsync(userId, requestorId, isModerator);

        // Приватность по настройкам
        if (!isOwner && !isModerator && revieweeSettings != null)
        {
            if (!revieweeSettings.ShowRatingsToOthers)
            {
                foreach (var r in reviews)
                {
                    r.LeadRatings = null;
                    r.FollowRatings = null;
                }
            }
            if (!revieweeSettings.ShowTextReviewsToOthers)
            {
                foreach (var r in reviews)
                {
                    r.TextReview = null;
                }
            }
        }

        // контент «Red» и «Pending» скрываем для посторонних, но статусы показываем всем
        if (!isOwner && !isModerator)
        {
            foreach (var r in reviews)
            {
                var level = (r.ModerationLevel ?? "Pending");
                if (level == "Red" || level == "Pending")
                {
                    r.LeadRatings = null;
                    r.FollowRatings = null;
                    r.TextReview = null;
                }
            }
        }

        return Ok(reviews);
    }

    [HttpGet("mine/given")]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetMyGiven()
    {
        var me = await _userManager.GetUserAsync(User);
        if (me == null) return Unauthorized();

        // Берём мои отзывы как автора; приватность тут не применяем, т.к. это «мои» отзывы
        var list = await _db.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .Where(r => r.ReviewerId == me.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var result = new List<ReviewDto>();
        foreach (var r in list)
        {
            result.Add(new ReviewDto
            {
                Id = r.Id,
                ReviewerId = r.ReviewerId,
                RevieweeId = r.RevieweeId,
                ReviewerName = r.IsAnonymous ? "Анонимный пользователь" : $"{r.Reviewer.FirstName} {r.Reviewer.LastName}",
                RevieweeName = $"{r.Reviewee.FirstName} {r.Reviewee.LastName}",
                EventId = r.EventId,
                EventName = r.Event?.Name,
                LeadRatings = ReviewService.TryDeserialize<Dictionary<string, int>>(r.LeadRatings),
                FollowRatings = ReviewService.TryDeserialize<Dictionary<string, int>>(r.FollowRatings),
                TextReview = r.TextReview,
                Tags = ReviewService.TryDeserialize<List<string>>(r.Tags),
                IsAnonymous = r.IsAnonymous,
                CreatedAt = r.CreatedAt,
                ModerationLevel = r.ModerationLevel.ToString(),
                ModerationSource = r.ModerationSource.ToString(),
                ModeratedAt = r.ModeratedAt,
                ModerationReason = r.ModerationReason
            });
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ReviewDto>> CreateReview([FromBody] CreateReviewDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var review = await _reviewService.CreateReviewAsync(currentUser.Id, model);

        // Если только звёзды (без текста) — считаем сразу Green, без LLM
        bool hasAnyStars =
            (model.LeadRatings != null && model.LeadRatings.Any(kv => kv.Value >= 1 && kv.Value <= 5)) ||
            (model.FollowRatings != null && model.FollowRatings.Any(kv => kv.Value >= 1 && kv.Value <= 5));
        bool hasText = !string.IsNullOrWhiteSpace(model.TextReview);

        if (hasAnyStars && !hasText)
        {
            var entity = await _db.Reviews.FindAsync(review.Id);
            if (entity != null)
            {
                entity.ModerationLevel = ModerationLevel.Green;
                entity.ModerationSource = ModerationSource.None;
                entity.ModeratedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            _db.ModerationJobs.Add(new ModerationJob
            {
                TargetType = "Review",
                TargetId = review.Id,
                Status = "Pending"
            });
            await _db.SaveChangesAsync();

            await _moderationQueue.EnqueueAsync(new ModerationMessage
            {
                TargetType = "Review",
                TargetId = review.Id
            });
        }

        return CreatedAtAction(nameof(GetReviews), review);
    }
}