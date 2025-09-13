using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class Event
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [Required]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? CoverImagePath { get; set; }  // NEW: путь в объектном хранилище (original)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User Creator { get; set; } = null!;
    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<EventReview> EventReviews { get; set; } = new List<EventReview>();
}