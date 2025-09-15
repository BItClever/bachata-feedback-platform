using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class ModerationJob
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string TargetType { get; set; } = "Review"; // "Review" | "EventReview"

    public int TargetId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending | Processing | Done | Error

    public int Attempts { get; set; } = 0;

    [MaxLength(500)]
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}