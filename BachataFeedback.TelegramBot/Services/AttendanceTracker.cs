using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BachataFeedback.TelegramBot.Services;

/// <summary>
/// Создаёт и обновляет записи Attendance на основе голосования в Telegram poll
/// или нажатия кнопки "Хочу прийти" в зеркальных публикациях.
/// Обеспечивает дедупликацию: один человек — одна запись на Occurrence.
/// </summary>
public class AttendanceTracker
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AttendanceTracker> _logger;

    public AttendanceTracker(ApplicationDbContext db, ILogger<AttendanceTracker> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Обработка ответа на poll (из PollAnswerHandler).
    /// optionIds: массив выбранных вариантов (0 = "Иду", 1 = "Не иду" / пусто = отзыв голоса).
    /// </summary>
    public async Task TrackPollAnswerAsync(
        string telegramPollId,
        long telegramUserId,
        string? telegramUsername,
        string? displayName,
        int[] optionIds,
        CancellationToken ct)
    {
        // Находим publication по poll_id
        var publication = await _db.OccurrencePublications
            .Include(op => op.Occurrence)
            .FirstOrDefaultAsync(op => op.TelegramPollId == telegramPollId && op.IsVotingSource, ct);

        if (publication == null)
        {
            _logger.LogWarning("[AttendanceTracker] PollId={PollId} not found in OccurrencePublications", telegramPollId);
            return;
        }

        var occurrenceId = publication.OccurrenceId;

        // Определяем статус на основе выбора (option 0 = "Иду", option 1 = "Не иду")
        // Если optionIds пустой — пользователь отозвал голос
        string status;
        if (optionIds.Length == 0)
        {
            // Отозвал голос — убираем запись
            await RemoveAttendanceAsync(occurrenceId, telegramUserId, ct);
            return;
        }

        status = optionIds[0] == 0 ? AttendanceStatus.Going : AttendanceStatus.NotGoing;

        await UpsertAttendanceAsync(
            occurrenceId, telegramUserId, telegramUsername, displayName,
            status, AttendanceSource.TelegramPoll, ct);
    }

    /// <summary>
    /// Обработка нажатия кнопки "Хочу прийти" / "Не приду" (из CallbackQueryHandler).
    /// </summary>
    public async Task TrackButtonActionAsync(
        int occurrenceId,
        long telegramUserId,
        string? telegramUsername,
        string? displayName,
        string status,
        CancellationToken ct)
    {
        await UpsertAttendanceAsync(
            occurrenceId, telegramUserId, telegramUsername, displayName,
            status, AttendanceSource.TelegramButton, ct);
    }

    private async Task UpsertAttendanceAsync(
        int occurrenceId,
        long telegramUserId,
        string? username,
        string? displayName,
        string status,
        string source,
        CancellationToken ct)
    {
        // Проверяем, есть ли уже запись по telegramUserId
        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.OccurrenceId == occurrenceId && a.TelegramUserId == telegramUserId, ct);

        if (existing != null)
        {
            existing.Status = status;
            existing.Source = source;
            existing.TelegramUsername = username;
            existing.TelegramDisplayName = displayName;
            existing.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation(
                "[AttendanceTracker] Updated attendance for TgUser={UserId} on Occurrence={OccId}: {Status}",
                telegramUserId, occurrenceId, status);
        }
        else
        {
            // Пытаемся связать с пользователем платформы по TelegramId
            var platformUser = await _db.Users
                .Where(u => u.TelegramId == telegramUserId)
                .Select(u => new { u.Id })
                .FirstOrDefaultAsync(ct);

            _db.Attendances.Add(new Attendance
            {
                OccurrenceId = occurrenceId,
                TelegramUserId = telegramUserId,
                TelegramUsername = username,
                TelegramDisplayName = displayName,
                UserId = platformUser?.Id,
                Status = status,
                Source = source,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "[AttendanceTracker] Created attendance for TgUser={UserId} on Occurrence={OccId}: {Status}",
                telegramUserId, occurrenceId, status);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task RemoveAttendanceAsync(int occurrenceId, long telegramUserId, CancellationToken ct)
    {
        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.OccurrenceId == occurrenceId && a.TelegramUserId == telegramUserId, ct);

        if (existing != null)
        {
            _db.Attendances.Remove(existing);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "[AttendanceTracker] Removed attendance for TgUser={UserId} on Occurrence={OccId}",
                telegramUserId, occurrenceId);
        }
    }

    /// <summary>
    /// Возвращает статистику attendance для отображения в боте.
    /// </summary>
    public async Task<AttendanceSummary> GetSummaryAsync(int occurrenceId, CancellationToken ct)
    {
        var attendances = await _db.Attendances
            .Where(a => a.OccurrenceId == occurrenceId)
            .ToListAsync(ct);

        return new AttendanceSummary
        {
            Total = attendances.Count(a => a.Status == AttendanceStatus.Going),
            NotGoing = attendances.Count(a => a.Status == AttendanceStatus.NotGoing),
            Leads = attendances.Count(a => a.Status == AttendanceStatus.Going && a.DancerRole == "lead"),
            Follows = attendances.Count(a => a.Status == AttendanceStatus.Going && a.DancerRole == "follow"),
        };
    }
}

public record AttendanceSummary
{
    public int Total { get; init; }
    public int NotGoing { get; init; }
    public int Leads { get; init; }
    public int Follows { get; init; }
}
