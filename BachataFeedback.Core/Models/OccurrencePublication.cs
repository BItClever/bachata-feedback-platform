using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

/// <summary>
/// Конкретная публикация Occurrence в Telegram-чат.
/// Одно занятие/ивент может иметь несколько публикаций в разных чатах.
/// </summary>
public class OccurrencePublication
{
    public int Id { get; set; }

    public int OccurrenceId { get; set; }

    public long TelegramChatId { get; set; }

    /// <summary>
    /// Telegram message_id сообщения/poll в чате (null если ещё не отправлено)
    /// </summary>
    public long? TelegramMessageId { get; set; }

    /// <summary>
    /// Telegram poll_id (null для mirror-публикаций без poll'а)
    /// </summary>
    [MaxLength(100)]
    public string? TelegramPollId { get; set; }

    /// <summary>
    /// canonical_poll — основной poll с голосованием (один на Occurrence)
    /// mirror         — зеркальное сообщение с кнопками (без нового poll'а)
    /// support_request — объявление о нехватке саппорта
    /// </summary>
    [MaxLength(30)]
    public string PublicationType { get; set; } = PublicationTypes.Mirror;

    /// <summary>
    /// Только одна публикация на Occurrence должна быть IsVotingSource = true.
    /// Именно из неё берутся attendance-данные.
    /// </summary>
    public bool IsVotingSource { get; set; } = false;

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Occurrence Occurrence { get; set; } = null!;
    public TelegramChat TelegramChat { get; set; } = null!;
}

public static class PublicationTypes
{
    public const string CanonicalPoll = "canonical_poll";
    public const string Mirror = "mirror";
    public const string SupportRequest = "support_request";
}
