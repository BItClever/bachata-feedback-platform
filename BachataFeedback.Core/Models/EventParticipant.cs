using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class EventParticipant
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public int EventId { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public bool IsConfirmed { get; set; } = false; // Подтверждение присутствия

    // Navigation properties
    public User User { get; set; } = null!;
    public Event Event { get; set; } = null!;
}