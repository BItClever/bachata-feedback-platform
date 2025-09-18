using BachataFeedback.Api.Data;
using BachataFeedback.Api.Services;
using BachataFeedback.Api.Services.Moderation;
using BachataFeedback.Core.DTOs;
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
        var reviews = await _reviewService.GetAllReviewsAsync(requestorId);
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

        var reviews = await _reviewService.GetUserReviewsAsync(userId, requestorId);

        bool isOwner = currentUserId == userId;
        bool isModerator = User.IsInRole("Admin") || User.IsInRole("Moderator");

        // Приватность по настройкам (как было)
        if (!isOwner && revieweeSettings != null)
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

    [HttpPost]
    public async Task<ActionResult<ReviewDto>> CreateReview([FromBody] CreateReviewDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var review = await _reviewService.CreateReviewAsync(currentUser.Id, model);
        // Создаем ModerationJob и публикуем в очередь
        _db.ModerationJobs.Add(new ModerationJob
        {
            TargetType = "Review",
            TargetId = review.Id, // где review — это reviewDto.Id
            Status = "Pending"
        });
        await _db.SaveChangesAsync();

        await _moderationQueue.EnqueueAsync(new ModerationMessage
        {
            TargetType = "Review",
            TargetId = review.Id
        });

        return CreatedAtAction(nameof(GetReviews), review);
    }
}