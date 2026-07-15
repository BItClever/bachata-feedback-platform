using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

/// <summary>
/// Telegram-чат, в котором работает бот.
/// Регистрируется вручную администратором.
/// </summary>
public class TelegramChat
{
    /// <summary>
    /// Telegram chat_id (может быть отрицательным для групп/каналов)
    /// </summary>
    public long ChatId { get; set; }

    /// <summary>
    /// group | supergroup | channel | private
    /// </summary>
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Роль этого чата в системе.
    /// group_primary   — основной чат конкретной танцевальной группы
    /// all_lessons_feed — канал со всеми занятиями (для внешних учеников)
    /// flood_chat      — общая флудилка (300+ человек)
    /// events_chat     — канал/чат для общих ивентов (вечеринки, поездки)
    /// admin_space     — служебный чат для администраторов
    /// </summary>
    [MaxLength(30)]
    public string Purpose { get; set; } = TelegramChatPurpose.FloodChat;

    /// <summary>
    /// Привязка к танцевальной группе — только для Purpose = group_primary
    /// </summary>
    public int? DanceGroupId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public DanceGroup? DanceGroup { get; set; }
    public ICollection<OccurrencePublication> Publications { get; set; } = new List<OccurrencePublication>();
}

public static class TelegramChatPurpose
{
    public const string GroupPrimary = "group_primary";
    public const string AllLessonsFeed = "all_lessons_feed";
    public const string FloodChat = "flood_chat";
    public const string EventsChat = "events_chat";
    public const string AdminSpace = "admin_space";

    public static readonly string[] All = [GroupPrimary, AllLessonsFeed, FloodChat, EventsChat, AdminSpace];
}
