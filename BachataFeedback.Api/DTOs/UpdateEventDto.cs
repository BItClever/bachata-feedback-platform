using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Api.DTOs;

public class UpdateEventDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    public DateTime? Date { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}