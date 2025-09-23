using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class EventPhoto
{
    public int Id { get; set; }

    [Required]
    public int EventId { get; set; }

    [Required]
    public string UploaderId { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty; // events/{eventId}/photos/{photoId}/original.jpg

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Event Event { get; set; } = null!;
    public User Uploader { get; set; } = null!;
}
