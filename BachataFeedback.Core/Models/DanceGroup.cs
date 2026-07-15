using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class DanceGroup
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Порядок отображения в списках
    /// </summary>
    public int SortOrder { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Occurrence> Occurrences { get; set; } = new List<Occurrence>();
    public ICollection<TelegramChat> Chats { get; set; } = new List<TelegramChat>();
}
