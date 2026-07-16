namespace BachataFeedback.Core.Models;

public enum AnalysisJobType
{
    Summary,
    UserProfile,
    ChatAnalytics
}

public enum AnalysisJobStatus
{
    Pending,
    Processing,
    Done,
    Failed
}

/// <summary>
/// Задача LLM-аналитики чата: summary, профиль пользователя или общая аналитика.
/// Создаётся ботом при получении команды, обрабатывается Worker'ом, доставляется обратно в чат.
/// </summary>
public class AnalysisJob
{
    public int Id { get; set; }

    public AnalysisJobType Type { get; set; }
    public AnalysisJobStatus Status { get; set; } = AnalysisJobStatus.Pending;

    /// <summary>Чат, в который нужно отправить результат.</summary>
    public long ReplyToChatId { get; set; }

    /// <summary>Количество сообщений для анализа.</summary>
    public int MessageCount { get; set; }

    /// <summary>Для UserProfile — telegram_user_id цели.</summary>
    public long? TargetTelegramUserId { get; set; }

    /// <summary>Имя/ник целевого пользователя (для отображения в ответе).</summary>
    public string? TargetUserDisplayName { get; set; }

    /// <summary>Telegram user_id того, кто запросил анализ.</summary>
    public long RequestedByUserId { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>Результат от LLM (текст для отправки в чат).</summary>
    public string? ResultText { get; set; }

    /// <summary>Флаг: результат отправлен в Telegram.</summary>
    public bool SentToChat { get; set; }

    /// <summary>Сообщение об ошибке (при Status=Failed).</summary>
    public string? ErrorMessage { get; set; }
}
