namespace BachataFeedback.Core.Models;

/// <summary>
/// LLM-профиль участника чата, строится по его истории сообщений.
/// Обновляется при каждом новом запросе /profile.
/// </summary>
public class UserChatProfile
{
    public int Id { get; set; }

    public long TelegramUserId { get; set; }
    public long TelegramChatId { get; set; }

    public string? Username { get; set; }
    public string? DisplayName { get; set; }

    /// <summary>Количество проанализированных сообщений.</summary>
    public int AnalyzedMessagesCount { get; set; }

    /// <summary>JSON с профилем от LLM (activity_level, main_topics, toxicity_score, tone, etc.).</summary>
    public string? AnalysisJson { get; set; }

    /// <summary>Краткое текстовое резюме профиля (для отображения в боте).</summary>
    public string? Summary { get; set; }

    public DateTime LastAnalyzedAt { get; set; }

    // Навигационное свойство
    public TelegramChat? TelegramChat { get; set; }
}
