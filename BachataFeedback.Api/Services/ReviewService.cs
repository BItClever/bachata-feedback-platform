using BachataFeedback.Api.Data;
using BachataFeedback.Api.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BachataFeedback.Api.Services;

public interface IReviewService
{
    Task<IEnumerable<ReviewDto>> GetAllReviewsAsync(string? requestorId, bool isModerator = false);
    Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId, string? requestorId, bool isModerator = false);
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

    private async Task<ReviewDto> MapToReviewDtoWithPrivacyAsync(Review review, string? requestorId, bool isModerator)
    {
        var dto = MapToReviewDto(review);

        // Если текущий — владелец профиля (reviewee) — показываем всё
        if (requestorId != null && review.RevieweeId == requestorId)
            return dto;

        // Скрываем токсичный/непроверенный контент для посторонних, если не модератор
        if (!isModerator)
        {
            var level = review.ModerationLevel;
            if (level == ModerationLevel.Red || level == ModerationLevel.Pending)
            {
                dto.LeadRatings = null;
                dto.FollowRatings = null;
                dto.TextReview = null;
            }
        }

        // Приватность по настройкам
        var settings = await _context.UserSettings.FindAsync(review.RevieweeId);
        bool showRatings = settings?.ShowRatingsToOthers ?? true;
        bool showText = settings?.ShowTextReviewsToOthers ?? true;

        if (!showRatings)
        {
            dto.LeadRatings = null;
            dto.FollowRatings = null;
        }
        if (!showText)
        {
            dto.TextReview = null;
        }

        return dto;
    }

    public async Task<IEnumerable<ReviewDto>> GetAllReviewsAsync(string? requestorId, bool isModerator = false)
    {
        var reviewsData = await _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var result = new List<ReviewDto>();
        foreach (var r in reviewsData)
            result.Add(await MapToReviewDtoWithPrivacyAsync(r, requestorId, isModerator));
        return result;
    }

    public async Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId, string? requestorId, bool isModerator = false)
    {
        var reviewsData = await _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .Where(r => r.RevieweeId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var result = new List<ReviewDto>();
        foreach (var r in reviewsData)
            result.Add(await MapToReviewDtoWithPrivacyAsync(r, requestorId, isModerator));
        return result;
    }

    public async Task<ReviewDto?> CreateReviewAsync(string reviewerId, CreateReviewDto model)
    {
        if (reviewerId == model.RevieweeId)
            throw new ApplicationException("Cannot review yourself");

        var reviewee = await _context.Users
            .Include(u => u.Settings)
            .FirstOrDefaultAsync(u => u.Id == model.RevieweeId);

        if (reviewee == null)
            throw new KeyNotFoundException("Reviewee not found");

        if (reviewee.Settings != null && !reviewee.Settings.AllowReviews)
            throw new ApplicationException("User does not allow reviews");

        if (model.IsAnonymous && reviewee.Settings != null && !reviewee.Settings.AllowAnonymousReviews)
            throw new ApplicationException("Anonymous reviews are not allowed by this user");

        // Проверка участия в событии (если указан eventId)
        if (model.EventId.HasValue)
        {
            var eventExists = await _context.Events.AnyAsync(e => e.Id == model.EventId.Value);
            if (!eventExists)
                throw new KeyNotFoundException("Event not found");

            var canReview = await CanUserReviewAsync(reviewerId, model.RevieweeId, model.EventId);
            if (!canReview)
                throw new ApplicationException("Both users must have participated in the event");
        }

        // Ограничение частоты: один не-ивентовый отзыв раз в 24 часа на одного пользователя
        //if (model.EventId == null)
        //{
        //    var dayAgo = DateTime.UtcNow.AddDays(-1);
        //    var recent = await _context.Reviews
        //        .Where(r => r.ReviewerId == reviewerId
        //                 && r.RevieweeId == model.RevieweeId
        //                 && r.EventId == null
        //                 && r.CreatedAt >= dayAgo)
        //        .AnyAsync();
        //    if (recent)
        //        throw new ApplicationException("You can leave a non-event review for this user once every 24 hours");
        //}

        // Санитизация и валидация рейтингов (1..5), игнорируем нули/мусор
        static Dictionary<string, int>? SanitizeRatings(Dictionary<string, int>? src)
        {
            if (src == null) return null;
            var filtered = src
                .Where(kv => kv.Value >= 1 && kv.Value <= 5)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return filtered.Count > 0 ? filtered : null;
        }

        var lead = SanitizeRatings(model.LeadRatings);
        var follow = SanitizeRatings(model.FollowRatings);

        var review = new Review
        {
            ReviewerId = reviewerId,
            RevieweeId = model.RevieweeId,
            EventId = model.EventId,
            LeadRatings = lead != null && lead.Count > 0 ? JsonSerializer.Serialize(lead) : null,
            FollowRatings = follow != null && follow.Count > 0 ? JsonSerializer.Serialize(follow) : null,
            TextReview = string.IsNullOrWhiteSpace(model.TextReview) ? null : model.TextReview,
            Tags = (model.Tags != null && model.Tags.Count > 0) ? JsonSerializer.Serialize(model.Tags) : null,
            IsAnonymous = model.IsAnonymous
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        await _context.Entry(review).Reference(r => r.Reviewer).LoadAsync();
        await _context.Entry(review).Reference(r => r.Reviewee).LoadAsync();
        if (review.EventId.HasValue)
            await _context.Entry(review).Reference(r => r.Event).LoadAsync();

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
            LeadRatings = TryDeserialize<Dictionary<string, int>>(review.LeadRatings),
            FollowRatings = TryDeserialize<Dictionary<string, int>>(review.FollowRatings),
            Tags = TryDeserialize<List<string>>(review.Tags),
            TextReview = review.TextReview,
            IsAnonymous = review.IsAnonymous,
            CreatedAt = review.CreatedAt,
            ModerationLevel = review.ModerationLevel.ToString(),
            ModerationSource = review.ModerationSource.ToString(),
            ModeratedAt = review.ModeratedAt,
            ModerationReason = review.ModerationReason,
            ModerationReasonRu = review.ModerationReasonRu,
            ModerationReasonEn = review.ModerationReasonEn,
        };
    }

    public static T? TryDeserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}