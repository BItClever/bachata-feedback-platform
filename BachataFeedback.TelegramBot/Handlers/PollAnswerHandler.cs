using BachataFeedback.TelegramBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace BachataFeedback.TelegramBot.Handlers;

/// <summary>
/// Обрабатывает poll_answer — ответ пользователя на неанонимный Telegram poll.
/// Работает только для poll'ов, отправленных самим ботом.
/// </summary>
public class PollAnswerHandler
{
    private readonly AttendanceTracker _tracker;
    private readonly ILogger<PollAnswerHandler> _logger;

    public PollAnswerHandler(AttendanceTracker tracker, ILogger<PollAnswerHandler> logger)
    {
        _tracker = tracker;
        _logger = logger;
    }

    public async Task HandleAsync(PollAnswer answer, CancellationToken ct)
    {
        var userId = answer.User?.Id ?? 0;
        if (userId == 0)
        {
            _logger.LogWarning("[PollAnswerHandler] Received answer without user info");
            return;
        }

        var username = answer.User?.Username;
        var displayName = answer.User?.FirstName + (string.IsNullOrEmpty(answer.User?.LastName)
            ? ""
            : " " + answer.User.LastName);

        _logger.LogInformation(
            "[PollAnswerHandler] User={UserId} (@{Username}) answered poll={PollId} with options=[{Options}]",
            userId, username, answer.PollId, string.Join(",", answer.OptionIds));

        await _tracker.TrackPollAnswerAsync(
            telegramPollId: answer.PollId,
            telegramUserId: userId,
            telegramUsername: username,
            displayName: displayName.Trim(),
            optionIds: answer.OptionIds,
            ct: ct);
    }
}
