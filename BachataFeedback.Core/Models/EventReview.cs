using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class EventReview
{
    public int Id { get; set; }

    [Required]
    public string ReviewerId { get; set; } = string.Empty;

    [Required]
    public int EventId { get; set; }

    // JSON: {"location": 4, "music": 5, "crowd": 4, "organization": 5}
    [MaxLength(2000)]
    public string? Ratings { get; set; }

    [MaxLength(1000)]
    public string? TextReview { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; } // JSON array

    public bool IsAnonymous { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Moderation
    public ModerationLevel ModerationLevel { get; set; } = ModerationLevel.Pending;
    public ModerationSource ModerationSource { get; set; } = ModerationSource.None;
    public DateTime? ModeratedAt { get; set; }
    [MaxLength(300)]
    public string? ModerationReason { get; set; }
    [MaxLength(300)]
    public string? ModerationReasonRu { get; set; }

    [MaxLength(300)]
    public string? ModerationReasonEn { get; set; }

    // Navigation
    public User Reviewer { get; set; } = null!;
    public Event Event { get; set; } = null!;
}