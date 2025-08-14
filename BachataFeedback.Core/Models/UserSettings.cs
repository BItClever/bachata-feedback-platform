using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

public class UserSettings
{
    [Key]
    public string UserId { get; set; } = string.Empty;

    public bool AllowReviews { get; set; } = true;
    public bool ShowRatingsToOthers { get; set; } = true;
    public bool ShowTextReviewsToOthers { get; set; } = true;
    public bool AllowAnonymousReviews { get; set; } = true;
    public bool ShowPhotosToGuests { get; set; } = true;

    public User User { get; set; } = null!;
}