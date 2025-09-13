using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.DTOs;

public class CreateEventDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class EventDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? CreatorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ParticipantCount { get; set; }
    public bool IsUserParticipating { get; set; }

    public string? CoverImageSmallUrl { get; set; }
    public string? CoverImageLargeUrl { get; set; }
}