namespace BachataFeedback.Api.Services.Moderation
{
    public interface IModerationQueue
    {
        Task EnqueueAsync(ModerationMessage message, CancellationToken ct = default);
    }
}
