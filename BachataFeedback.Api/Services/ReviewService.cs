using BachataFeedback.Api.Data;
using BachataFeedback.Core.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BachataFeedback.Api.Services;

public interface IReviewService
{
    Task<IEnumerable<ReviewDto>> GetAllReviewsAsync();
    Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId);
    Task<ReviewDto?> CreateReviewAsync(string reviewerId, CreateReviewDto model);
    Task<bool> CanUserReviewAsync(string reviewerId, string revieweeId, int? eventId);
}

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _context;

    public ReviewService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ReviewDto>> GetAllReviewsAsync()
    {
        var reviewsData = await _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return MapToReviewDtos(reviewsData);
    }

    public async Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId)
    {
        var reviewsData = await _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .Where(r => r.RevieweeId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return MapToReviewDtos(reviewsData);
    }

    public async Task<ReviewDto?> CreateReviewAsync(string reviewerId, CreateReviewDto model)
    {
        // Проверки
        if (reviewerId == model.RevieweeId)
            throw new ApplicationException("Cannot review yourself");

        var reviewee = await _context.Users.FindAsync(model.RevieweeId);
        if (reviewee == null)
            throw new KeyNotFoundException("Reviewee not found");

        // Проверка участия в событии
        if (model.EventId.HasValue)
        {
            var eventExists = await _context.Events.AnyAsync(e => e.Id == model.EventId.Value);
            if (!eventExists)
                throw new KeyNotFoundException("Event not found");

            var canReview = await CanUserReviewAsync(reviewerId, model.RevieweeId, model.EventId);
            if (!canReview)
                throw new ApplicationException("Both users must have participated in the event");
        }

        var review = new Review
        {
            ReviewerId = reviewerId,
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

        // Загружаем связанные данные для ответа
        await _context.Entry(review)
            .Reference(r => r.Reviewer)
            .LoadAsync();
        await _context.Entry(review)
            .Reference(r => r.Reviewee)
            .LoadAsync();
        if (review.EventId.HasValue)
        {
            await _context.Entry(review)
                .Reference(r => r.Event)
                .LoadAsync();
        }

        var reviewDto = MapToReviewDto(review);
        return reviewDto;
    }

    public async Task<bool> CanUserReviewAsync(string reviewerId, string revieweeId, int? eventId)
    {
        if (!eventId.HasValue) return true;

        var reviewerParticipated = await _context.EventParticipants
            .AnyAsync(ep => ep.EventId == eventId.Value && ep.UserId == reviewerId);

        var revieweeParticipated = await _context.EventParticipants
            .AnyAsync(ep => ep.EventId == eventId.Value && ep.UserId == revieweeId);

        return reviewerParticipated && revieweeParticipated;
    }

    private static IEnumerable<ReviewDto> MapToReviewDtos(IEnumerable<Review> reviews)
    {
        return reviews.Select(MapToReviewDto);
    }

    private static ReviewDto MapToReviewDto(Review review)
    {
        return new ReviewDto
        {
            Id = review.Id,
            ReviewerId = review.ReviewerId,
            RevieweeId = review.RevieweeId,
            ReviewerName = review.IsAnonymous ? "Анонимный пользователь" :
                         $"{review.Reviewer.FirstName} {review.Reviewer.LastName}",
            RevieweeName = $"{review.Reviewee.FirstName} {review.Reviewee.LastName}",
            EventId = review.EventId,
            EventName = review.Event?.Name,
            LeadRatings = !string.IsNullOrEmpty(review.LeadRatings) ?
                         JsonSerializer.Deserialize<Dictionary<string, int>>(review.LeadRatings) : null,
            FollowRatings = !string.IsNullOrEmpty(review.FollowRatings) ?
                           JsonSerializer.Deserialize<Dictionary<string, int>>(review.FollowRatings) : null,
            TextReview = review.TextReview,
            Tags = !string.IsNullOrEmpty(review.Tags) ?
                   JsonSerializer.Deserialize<List<string>>(review.Tags) : null,
            IsAnonymous = review.IsAnonymous,
            CreatedAt = review.CreatedAt
        };
    }
}