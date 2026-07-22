using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

/// <summary>
/// Аналитический лог каждого действия пользователя с опросом в Telegram.
/// Фиксируется каждое голосование, изменение голоса, отзыв голоса.
/// Не заменяет Attendance, а дополняет для future-аналитики.
/// </summary>
public class PollVoteLog
{
    public int Id { get; set; }

    /// <summary>
    /// ID пользователя Telegram, который совершил действие
    /// </summary>
    public long TelegramUserId { get; set; }

    /// <summary>
    /// ID poll'а в Telegram
    /// </summary>
    [MaxLength(64)]
    public string TelegramPollId { get; set; } = string.Empty;

    /// <summary>
    /// Индекс выбранного варианта: 0=парни, 1=девушки, 2=тренеры, 3=не иду
    /// null = отзыв голоса (retract)
    /// </summary>
    public int? OptionIndex { get; set; }

    /// <summary>
    /// action_type: voted | changed | retracted
    /// </summary>
    [MaxLength(20)]
    public string ActionType { get; set; } = "voted";

    /// <summary>
    /// Telegram username на момент действия
    /// </summary>
    [MaxLength(64)]
    public string? TelegramUsername { get; set; }

    /// <summary>
    /// Отображаемое имя из Telegram
    /// </summary>
    [MaxLength(200)]
    public string? TelegramDisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}