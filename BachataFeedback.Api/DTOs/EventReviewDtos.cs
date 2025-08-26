using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Api.DTOs
{
    public class CreateEventReviewDto
    {
        [Required]
        public int EventId { get; set; }

        public Dictionary<string, int>? Ratings { get; set; } // {"location":5,...}

        [MaxLength(1000)]
        public string? TextReview { get; set; }

        public List<string>? Tags { get; set; }

        public bool IsAnonymous { get; set; } = false;
    }

    public class EventReviewDto
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string ReviewerId { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty; // "Anonymous" если IsAnonymous
        public Dictionary<string, int>? Ratings { get; set; }
        public string? TextReview { get; set; }
        public List<string>? Tags { get; set; }
        public bool IsAnonymous { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
