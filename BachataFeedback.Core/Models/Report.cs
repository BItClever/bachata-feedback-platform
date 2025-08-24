using BachataFeedback.Core.Models;
using System.ComponentModel.DataAnnotations;

public class Report
{
    public int Id { get; set; }

    [Required]
    public string ReporterId { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string TargetType { get; set; } = string.Empty; // "Review" или "Photo"

    public int TargetId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    // Navigation properties
    public User Reporter { get; set; } = null!;
}