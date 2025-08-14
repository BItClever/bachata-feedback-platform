using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class UserPhoto
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Caption { get; set; }

    public bool IsMain { get; set; } = false;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}