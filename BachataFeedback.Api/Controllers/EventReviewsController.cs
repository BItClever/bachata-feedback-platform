using BachataFeedback.Api.Data;
using BachataFeedback.Api.DTOs;
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
public class EventReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public EventReviewsController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("event/{eventId}")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<EventReviewDto>>> GetByEvent(int eventId)
    {
        var data = await _context.EventReviews
            .Include(er => er.Event)
            .Include(er => er.Reviewer)
            .Where(er => er.EventId == eventId)
            .OrderByDescending(er => er.CreatedAt)
            .ToListAsync();

        var result = data.Select(er => new EventReviewDto
        {
            Id = er.Id,
            EventId = er.EventId,
            EventName = er.Event.Name,
            ReviewerId = er.ReviewerId,
            ReviewerName = er.IsAnonymous ? "Anonymous" : $"{er.Reviewer.FirstName} {er.Reviewer.LastName}",
            Ratings = !string.IsNullOrEmpty(er.Ratings) ? JsonSerializer.Deserialize<Dictionary<string, int>>(er.Ratings) : null,
            TextReview = er.TextReview,
            Tags = !string.IsNullOrEmpty(er.Tags) ? JsonSerializer.Deserialize<List<string>>(er.Tags) : null,
            IsAnonymous = er.IsAnonymous,
            CreatedAt = er.CreatedAt
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EventReviewDto>> Create([FromBody] CreateEventReviewDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var ev = await _context.Events.FindAsync(model.EventId);
        if (ev == null)
            return BadRequest(new { success = false, message = "Event not found" });

        // Только участники могут оставлять отзыв о событии
        var isParticipant = await _context.EventParticipants
            .AnyAsync(ep => ep.EventId == model.EventId && ep.UserId == currentUser.Id);

        if (!isParticipant)
            return BadRequest(new { success = false, message = "Only event participants can leave a review" });

        static Dictionary<string, int>? SanitizeRatings(Dictionary<string, int>? src)
        {
            if (src == null) return null;
            var filtered = src.Where(kv => kv.Value >= 1 && kv.Value <= 5).ToDictionary(kv => kv.Key, kv => kv.Value);
            return filtered.Count > 0 ? filtered : null;
        }

        var ratings = SanitizeRatings(model.Ratings);

        var review = new EventReview
        {
            ReviewerId = currentUser.Id,
            EventId = model.EventId,
            Ratings = ratings != null ? JsonSerializer.Serialize(ratings) : null,
            TextReview = model.TextReview,
            Tags = model.Tags != null ? JsonSerializer.Serialize(model.Tags) : null,
            IsAnonymous = model.IsAnonymous
        };

        _context.EventReviews.Add(review);
        await _context.SaveChangesAsync();

        await _context.Entry(review).Reference(r => r.Event).LoadAsync();
        await _context.Entry(review).Reference(r => r.Reviewer).LoadAsync();

        var dto = new EventReviewDto
        {
            Id = review.Id,
            EventId = review.EventId,
            EventName = review.Event.Name,
            ReviewerId = review.ReviewerId,
            ReviewerName = review.IsAnonymous ? "Anonymous" : $"{currentUser.FirstName} {currentUser.LastName}",
            Ratings = ratings,
            TextReview = review.TextReview,
            Tags = model.Tags,
            IsAnonymous = review.IsAnonymous,
            CreatedAt = review.CreatedAt
        };

        return CreatedAtAction(nameof(GetByEvent), new { eventId = review.EventId }, dto);
    }
}