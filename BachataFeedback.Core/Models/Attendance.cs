using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

/// <summary>
/// Запись конкретного человека на конкретный Occurrence.
/// Источник истины — платформа, а не конкретный чат или poll.
/// Дедупликация: один человек может иметь только одну запись на Occurrence.
/// </summary>
public class Attendance
{
    public int Id { get; set; }

    public int OccurrenceId { get; set; }

    /// <summary>
    /// FK на зарегистрированного пользователя платформы (может быть null)
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Telegram user_id (для тех, кто не зарегистрирован на платформе)
    /// </summary>
    public long? TelegramUserId { get; set; }

    /// <summary>
    /// Telegram username для отображения (@username)
    /// </summary>
    [MaxLength(64)]
    public string? TelegramUsername { get; set; }

    /// <summary>
    /// Имя из Telegram (first_name) для отображения
    /// </summary>
    [MaxLength(100)]
    public string? TelegramDisplayName { get; set; }

    /// <summary>
    /// going | not_going | maybe | support_candidate | support_accepted | checked_in
    /// </summary>
    [MaxLength(30)]
    public string Status { get; set; } = AttendanceStatus.Going;

    /// <summary>
    /// telegram_poll | telegram_button | admin_manual | web
    /// </summary>
    [MaxLength(30)]
    public string Source { get; set; } = AttendanceSource.TelegramPoll;

    /// <summary>
    /// Роль на этом занятии: lead | follow | both | unknown
    /// </summary>
    [MaxLength(10)]
    public string? DancerRole { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Occurrence Occurrence { get; set; } = null!;
    public User? User { get; set; }
}

public static class AttendanceStatus
{
    public const string Going = "going";
    public const string NotGoing = "not_going";
    public const string Maybe = "maybe";
    public const string SupportCandidate = "support_candidate";
    public const string SupportAccepted = "support_accepted";
    public const string CheckedIn = "checked_in";
}

public static class AttendanceSource
{
    public const string TelegramPoll = "telegram_poll";
    public const string TelegramButton = "telegram_button";
    public const string AdminManual = "admin_manual";
    public const string Web = "web";
}
