using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Core.Models;

/// <summary>
/// Конкретное занятие или ивент — главная бизнес-сущность.
/// Чаты и poll'ы — лишь каналы доставки для этой сущности.
/// </summary>
public class Occurrence
{
    public int Id { get; set; }

    /// <summary>
    /// lesson | party | trip | practice
    /// </summary>
    [MaxLength(20)]
    public string Type { get; set; } = OccurrenceType.Lesson;

    /// <summary>
    /// Привязка к группе — обязательна для Type = lesson, опциональна для остальных
    /// </summary>
    public int? DanceGroupId { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    /// <summary>
    /// Человекочитаемое название — особенно важно для party/trip
    /// </summary>
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>
    /// Уровень (Beginner, Intermediate, Advanced, All) — опционально
    /// </summary>
    [MaxLength(30)]
    public string? Level { get; set; }

    /// <summary>
    /// Максимальное количество участников (null = без ограничений)
    /// </summary>
    public int? Capacity { get; set; }

    /// <summary>
    /// Целевой баланс парней (null = не отслеживается)
    /// </summary>
    public int? BalanceMales { get; set; }

    /// <summary>
    /// Целевой баланс девушек (null = не отслеживается)
    /// </summary>
    public int? BalanceFemales { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// draft | published | cancelled | completed
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = OccurrenceStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public DanceGroup? DanceGroup { get; set; }
    public ICollection<OccurrencePublication> Publications { get; set; } = new List<OccurrencePublication>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

public static class OccurrenceType
{
    public const string Lesson = "lesson";
    public const string Party = "party";
    public const string Trip = "trip";
    public const string Practice = "practice";
}

public static class OccurrenceStatus
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Cancelled = "cancelled";
    public const string Completed = "completed";
}
