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

    public async Task<IEnumerable<ReviewDto>> GetAllReviewsAsync(string? requestorId, bool isModerator = false)
    {
        var query = _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .OrderByDescending(r => r.CreatedAt);

        // Модераторы видят всё; остальные — только Green/Yellow + свои Pending/Red
        List<Review> reviewsData;
        if (isModerator)
        {
            reviewsData = await query.ToListAsync();
            return reviewsData.Select(MapToReviewDto);
        }

        reviewsData = await query
            .Where(r => r.ModerationLevel == ModerationLevel.Green
                     || r.ModerationLevel == ModerationLevel.Yellow
                     || r.ReviewerId == requestorId)
            .ToListAsync();

        // Батч-загрузка настроек приватности — исправляет N+1 запросов
        var revieweeIds = reviewsData.Select(r => r.RevieweeId).Distinct().ToList();
        var settingsMap = await _context.UserSettings
            .Where(s => revieweeIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId);

        var result = new List<ReviewDto>();
        foreach (var r in reviewsData)
        {
            var dto = MapToReviewDto(r);
            ApplyAnonymityMask(dto, r, requestorId);
            ApplyPrivacySettings(dto, r, requestorId, settingsMap);
            result.Add(dto);
        }
        return result;
    }

    public async Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId, string? requestorId, bool isModerator = false)
    {
        var query = _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Reviewee)
            .Include(r => r.Event)
            .Where(r => r.RevieweeId == userId)
            .OrderByDescending(r => r.CreatedAt);

        List<Review> reviewsData;
        if (isModerator)
        {
            reviewsData = await query.ToListAsync();
            return reviewsData.Select(MapToReviewDto);
        }

        reviewsData = await query
            .Where(r => r.ModerationLevel == ModerationLevel.Green
                     || r.ModerationLevel == ModerationLevel.Yellow
                     || r.ReviewerId == requestorId)
            .ToListAsync();

        // Для одного userId достаточно одного запроса настроек
        var settings = await _context.UserSettings.FindAsync(userId);
        var settingsMap = settings != null
            ? new Dictionary<string, UserSettings> { [userId] = settings }
            : new Dictionary<string, UserSettings>();

        bool isOwner = requestorId == userId;

        var result = new List<ReviewDto>();
        foreach (var r in reviewsData)
        {
            var dto = MapToReviewDto(r);
            ApplyAnonymityMask(dto, r, requestorId);
            if (!isOwner)
                ApplyPrivacySettings(dto, r, requestorId, settingsMap);
            result.Add(dto);
        }
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

        // Ограничение частоты: один не-событийный отзыв раз в 24 часа на одного пользователя
        if (model.EventId == null)
        {
            var dayAgo = DateTime.UtcNow.AddDays(-1);
            var recentExists = await _context.Reviews
                .Where(r => r.ReviewerId == reviewerId
                         && r.RevieweeId == model.RevieweeId
                         && r.EventId == null
                         && r.CreatedAt >= dayAgo)
                .AnyAsync();
            if (recentExists)
                throw new ApplicationException("You can leave a non-event review for this user once every 24 hours");
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

        return MapToReviewDto(review);
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

    // --- Private helpers ---

    /// <summary>
    /// Маскирует ReviewerId для анонимных отзывов, если запрашивающий — не автор.
    /// Предотвращает деанонимизацию через API.
    /// </summary>
    private static void ApplyAnonymityMask(ReviewDto dto, Review review, string? requestorId)
    {
        if (review.IsAnonymous && review.ReviewerId != requestorId)
            dto.ReviewerId = string.Empty;
    }

    /// <summary>
    /// Применяет настройки приватности reviewee: скрывает рейтинги/текст по его настройкам.
    /// Вызывать только для не-модераторов и не-владельцев профиля.
    /// </summary>
    private static void ApplyPrivacySettings(
        ReviewDto dto,
        Review review,
        string? requestorId,
        Dictionary<string, UserSettings> settingsMap)
    {
        // Владелец профиля всегда видит всё о своих отзывах
        if (review.RevieweeId == requestorId)
            return;

        if (!settingsMap.TryGetValue(review.RevieweeId, out var settings))
            return;

        if (!settings.ShowRatingsToOthers)
        {
            dto.LeadRatings = null;
            dto.FollowRatings = null;
        }
        if (!settings.ShowTextReviewsToOthers)
            dto.TextReview = null;
    }

    private static Dictionary<string, int>? SanitizeRatings(Dictionary<string, int>? src)
    {
        if (src == null) return null;
        var filtered = src
            .Where(kv => kv.Value >= 1 && kv.Value <= 5)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return filtered.Count > 0 ? filtered : null;
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
