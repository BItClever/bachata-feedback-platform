using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.DTOs;

public class CreateReviewDto
{
    [Required]
    public string RevieweeId { get; set; } = string.Empty;

    public int? EventId { get; set; }

    public Dictionary<string, int>? LeadRatings { get; set; }
    public Dictionary<string, int>? FollowRatings { get; set; }

    [MaxLength(1000)]
    public string? TextReview { get; set; }

    public List<string>? Tags { get; set; }

    public bool IsAnonymous { get; set; } = false;
}

public class ReviewDto
{
    public int Id { get; set; }
    public string ReviewerId { get; set; } = string.Empty;
    public string RevieweeId { get; set; } = string.Empty;
    public string? ReviewerName { get; set; }
    public string? RevieweeName { get; set; }
    public int? EventId { get; set; }
    public string? EventName { get; set; }
    public Dictionary<string, int>? LeadRatings { get; set; }
    public Dictionary<string, int>? FollowRatings { get; set; }
    public string? TextReview { get; set; }
    public List<string>? Tags { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
}