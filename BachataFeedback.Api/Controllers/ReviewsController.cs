using BachataFeedback.Api.Data;
using BachataFeedback.Core.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public ReviewsController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetReviews()
    {
        var reviewsData = await _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var reviews = reviewsData.Select(r => new ReviewDto
        {
            Id = r.Id,
            ReviewerId = r.ReviewerId,
            RevieweeId = r.RevieweeId,
            ReviewerName = r.IsAnonymous ? "Анонимный пользователь" :
                         r.Reviewer.FirstName + " " + r.Reviewer.LastName,
            RevieweeName = r.Reviewee.FirstName + " " + r.Reviewee.LastName,
            EventId = r.EventId,
            EventName = r.Event?.Name,
            LeadRatings = !string.IsNullOrEmpty(r.LeadRatings) ?
                         JsonSerializer.Deserialize<Dictionary<string, int>>(r.LeadRatings) : null,
            FollowRatings = !string.IsNullOrEmpty(r.FollowRatings) ?
                           JsonSerializer.Deserialize<Dictionary<string, int>>(r.FollowRatings) : null,
            TextReview = r.TextReview,
            Tags = !string.IsNullOrEmpty(r.Tags) ?
                   JsonSerializer.Deserialize<List<string>>(r.Tags) : null,
            IsAnonymous = r.IsAnonymous,
            CreatedAt = r.CreatedAt
        }).ToList();

        return Ok(reviews);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetUserReviews(string userId)
    {
        var reviewsData = await _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .Where(r => r.RevieweeId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var reviews = reviewsData.Select(r => new ReviewDto
        {
            Id = r.Id,
            ReviewerId = r.ReviewerId,
            RevieweeId = r.RevieweeId,
            ReviewerName = r.IsAnonymous ? "Анонимный пользователь" :
                         r.Reviewer.FirstName + " " + r.Reviewer.LastName,
            RevieweeName = r.Reviewee.FirstName + " " + r.Reviewee.LastName,
            EventId = r.EventId,
            EventName = r.Event?.Name,
            LeadRatings = !string.IsNullOrEmpty(r.LeadRatings) ?
                         JsonSerializer.Deserialize<Dictionary<string, int>>(r.LeadRatings) : null,
            FollowRatings = !string.IsNullOrEmpty(r.FollowRatings) ?
                           JsonSerializer.Deserialize<Dictionary<string, int>>(r.FollowRatings) : null,
            TextReview = r.TextReview,
            Tags = !string.IsNullOrEmpty(r.Tags) ?
                   JsonSerializer.Deserialize<List<string>>(r.Tags) : null,
            IsAnonymous = r.IsAnonymous,
            CreatedAt = r.CreatedAt
        }).ToList();

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

        // Проверяем, что пользователь не оставляет отзыв самому себе
        if (currentUser.Id == model.RevieweeId)
            return BadRequest(new { message = "Cannot review yourself" });

        // Проверяем, что reviewee существует
        var reviewee = await _context.Users.FindAsync(model.RevieweeId);
        if (reviewee == null)
            return BadRequest(new { message = "Reviewee not found" });

        // Если указано событие, проверяем участие обоих пользователей
        if (model.EventId.HasValue)
        {
            var eventExists = await _context.Events.AnyAsync(e => e.Id == model.EventId.Value);
            if (!eventExists)
                return BadRequest(new { message = "Event not found" });

            var reviewerParticipated = await _context.EventParticipants
                .AnyAsync(ep => ep.EventId == model.EventId.Value && ep.UserId == currentUser.Id);

            var revieweeParticipated = await _context.EventParticipants
                .AnyAsync(ep => ep.EventId == model.EventId.Value && ep.UserId == model.RevieweeId);

            if (!reviewerParticipated || !revieweeParticipated)
                return BadRequest(new { message = "Both users must have participated in the event" });
        }

        var review = new Review
        {
            ReviewerId = currentUser.Id,
            RevieweeId = model.RevieweeId,
            EventId = model.EventId,
            LeadRatings = model.LeadRatings != null ? JsonSerializer.Serialize(model.LeadRatings) : null,
            FollowRatings = model.FollowRatings != null ? JsonSerializer.Serialize(model.FollowRatings) : null,
            TextReview = model.TextReview,
            Tags = model.Tags != null ? JsonSerializer.Serialize(model.Tags) : null,
            IsAnonymous = model.IsAnonymous
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var reviewDto = new ReviewDto
        {
            Id = review.Id,
            ReviewerId = review.ReviewerId,
            RevieweeId = review.RevieweeId,
            ReviewerName = review.IsAnonymous ? "Анонимный пользователь" :
                         currentUser.FirstName + " " + currentUser.LastName,
            RevieweeName = reviewee.FirstName + " " + reviewee.LastName,
            EventId = review.EventId,
            LeadRatings = model.LeadRatings,
            FollowRatings = model.FollowRatings,
            TextReview = review.TextReview,
            Tags = model.Tags,
            IsAnonymous = review.IsAnonymous,
            CreatedAt = review.CreatedAt
        };

        return CreatedAtAction(nameof(GetReviews), reviewDto);
    }
}