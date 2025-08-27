using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class User : IdentityUser
{
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Nickname { get; set; }

    public DateTime? StartDancingDate { get; set; }

    [MaxLength(20)]
    public string? SelfAssessedLevel { get; set; }

    [MaxLength(200)]
    public string? Bio { get; set; }

    [MaxLength(500)]
    public string? DanceStyles { get; set; }

    public string? MainPhotoPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
    [MaxLength(10)]
    public string? DancerRole { get; set; } // "Lead", "Follow", "Both"

    // Navigation properties
    public UserSettings? Settings { get; set; }
    public ICollection<UserPhoto> Photos { get; set; } = new List<UserPhoto>();
    public ICollection<Review> ReviewsGiven { get; set; } = new List<Review>();
    public ICollection<Review> ReviewsReceived { get; set; } = new List<Review>();
    public ICollection<EventParticipant> EventParticipations { get; set; } = new List<EventParticipant>();
}