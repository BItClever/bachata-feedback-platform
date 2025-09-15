namespace BachataFeedback.Api.Services.Moderation
{
    public class ModerationMessage
    {
        public string TargetType { get; set; } = string.Empty; // "Review" | "EventReview"
        public int TargetId { get; set; }
    }

}
