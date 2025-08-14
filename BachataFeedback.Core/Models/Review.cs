using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class Review
{
    public int Id { get; set; }

    [Required]
    public string ReviewerId { get; set; } = string.Empty;

    [Required]
    public string RevieweeId { get; set; } = string.Empty;

    public int? EventId { get; set; } // Nullable - отзыв может быть общий или за конкретное событие

    [MaxLength(2000)]
    public string? LeadRatings { get; set; } // JSON: {"technique": 4, "musicality": 5, "leading": 4}

    [MaxLength(2000)]
    public string? FollowRatings { get; set; } // JSON: {"technique": 4, "musicality": 5, "following": 4}

    [MaxLength(1000)]
    public string? TextReview { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; } // JSON array: ["музыкальный", "техничный"]

    public bool IsAnonymous { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User Reviewer { get; set; } = null!;
    public User Reviewee { get; set; } = null!;
    public Event? Event { get; set; }
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}