namespace BachataFeedback.Core.Models;

/// <summary>
/// Хранит историю сообщений из Telegram-чатов для последующего LLM-анализа.
/// Бот должен быть администратором в чате для получения всех сообщений.
/// </summary>
public class ChatMessage
{
    public long Id { get; set; }

    /// <summary>Telegram chat_id источника.</summary>
    public long TelegramChatId { get; set; }

    /// <summary>Telegram message_id (уникален в рамках чата).</summary>
    public long TelegramMessageId { get; set; }

    /// <summary>Telegram user_id автора (null для анонимных/каналов).</summary>
    public long? TelegramUserId { get; set; }

    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Текст сообщения (null для медиа без подписи).</summary>
    public string? Text { get; set; }

    /// <summary>Время отправки (UTC).</summary>
    public DateTime SentAt { get; set; }

    /// <summary>Является ли пересланным сообщением.</summary>
    public bool IsForwarded { get; set; }

    /// <summary>Отправил ли бот это сообщение.</summary>
    public bool IsBot { get; set; }

    // Навигационное свойство
    public TelegramChat? TelegramChat { get; set; }
}
