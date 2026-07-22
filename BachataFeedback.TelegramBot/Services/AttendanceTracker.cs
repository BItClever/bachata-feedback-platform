using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BachataFeedback.TelegramBot.Services;

/// <summary>
/// Создаёт и обновляет записи Attendance на основе голосования в Telegram poll
/// или нажатия кнопки "Хочу прийти" в зеркальных публикациях.
///
/// Маппинг poll-опций (согласовано с OccurrencePublisher):
///   0 = парни (Going + Male)
///   1 = девушки (Going + Female)
///   2 = тренеры/организаторы (Going + Trainer)
///   3 = не иду (NotGoing)
///   пустой массив = отзыв голоса (Retracted)
///
/// Каждое действие логируется в PollVoteLog для аналитики.
/// Если голосует пользователь без профиля в системе — профиль создаётся автоматически.
/// Тренеры не учитываются в общем балансе на занятиях.
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
    /// optionIds: массив выбранных вариантов.
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

        // Определяем действие
        if (optionIds.Length == 0)
        {
            // Отзыв голоса
            await LogVoteActionAsync(telegramPollId, telegramUserId, telegramUsername, displayName,
                optionIndex: null, actionType: "retracted", ct);
            await RemoveAttendanceAsync(occurrenceId, telegramUserId, ct);
            return;
        }

        var selectedOption = optionIds[0];

        // Маппинг опции в статус и роль
        (string status, string? dancerRole) = MapOption(selectedOption);

        // Определяем тип действия (voted или changed)
        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.OccurrenceId == occurrenceId && a.TelegramUserId == telegramUserId, ct);

        var actionType = existing != null ? "changed" : "voted";
        await LogVoteActionAsync(telegramPollId, telegramUserId, telegramUsername, displayName,
            optionIndex: selectedOption, actionType: actionType, ct);

        // Авто-создание профиля, если пользователя нет в системе
        await EnsureUserProfileExistsAsync(telegramUserId, telegramUsername, displayName, ct);

        await UpsertAttendanceAsync(
            occurrenceId, telegramUserId, telegramUsername, displayName,
            status, dancerRole, AttendanceSource.TelegramPoll, ct);
    }

    /// <summary>
    /// Обработка нажатия inline-кнопки (из CallbackQueryHandler).
    /// Для кнопок роль не определяется — ставим Unknown.
    /// </summary>
    public async Task TrackButtonActionAsync(
        int occurrenceId,
        long telegramUserId,
        string? telegramUsername,
        string? displayName,
        string status,
        CancellationToken ct)
    {
        await EnsureUserProfileExistsAsync(telegramUserId, telegramUsername, displayName, ct);

        await UpsertAttendanceAsync(
            occurrenceId, telegramUserId, telegramUsername, displayName,
            status, DancerRoleAttendance.Unknown, AttendanceSource.TelegramButton, ct);
    }

    // ─── Маппинг опций ────────────────────────────────────────────────────────

    private static (string status, string? dancerRole) MapOption(int optionIndex)
    {
        return optionIndex switch
        {
            0 => (AttendanceStatus.Going, DancerRoleAttendance.Male),       // парни
            1 => (AttendanceStatus.Going, DancerRoleAttendance.Female),     // девушки
            2 => (AttendanceStatus.Going, DancerRoleAttendance.Trainer),    // тренеры/организаторы
            3 => (AttendanceStatus.NotGoing, DancerRoleAttendance.Unknown), // не иду
            _ => (AttendanceStatus.NotGoing, DancerRoleAttendance.Unknown)
        };
    }

    // ─── Логирование в PollVoteLog ────────────────────────────────────────────

    private async Task LogVoteActionAsync(
        string telegramPollId,
        long telegramUserId,
        string? username,
        string? displayName,
        int? optionIndex,
        string actionType,
        CancellationToken ct)
    {
        _db.PollVoteLogs.Add(new PollVoteLog
        {
            TelegramUserId = telegramUserId,
            TelegramPollId = telegramPollId,
            OptionIndex = optionIndex,
            ActionType = actionType,
            TelegramUsername = username,
            TelegramDisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    // ─── Авто-создание профиля ────────────────────────────────────────────────

    private async Task EnsureUserProfileExistsAsync(
        long telegramUserId,
        string? telegramUsername,
        string? displayName,
        CancellationToken ct)
    {
        // Проверяем, есть ли уже пользователь с таким TelegramId
        var exists = await _db.Users.AnyAsync(u => u.TelegramId == telegramUserId, ct);
        if (exists) return;

        // Создаём минимальный профиль
        var email = $"tg_{telegramUserId}@telegram.local";
        // Если такой email уже существует (коллизия) — не создаём дубль
        if (await _db.Users.AnyAsync(u => u.Email == email, ct)) return;

        var firstName = displayName?.Split(' ')[0] ?? telegramUsername ?? $"User{telegramUserId}";
        var lastName = displayName?.Contains(' ') == true
            ? displayName.Split(' ', 2)[1]
            : null;

        var username = $"tg_{telegramUserId}";
        var user = new User
        {
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            FirstName = firstName.Length > 50 ? firstName[..50] : firstName,
            LastName = lastName?.Length > 50 ? lastName[..50] : (lastName ?? string.Empty),
            TelegramId = telegramUserId,
            TelegramUsername = telegramUsername,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[AttendanceTracker] Auto-created user profile for Telegram user {TgUserId} (@{Username})",
            telegramUserId, telegramUsername);
    }

    // ─── Upsert / Remove Attendance ───────────────────────────────────────────

    private async Task UpsertAttendanceAsync(
        int occurrenceId,
        long telegramUserId,
        string? username,
        string? displayName,
        string status,
        string? dancerRole,
        string source,
        CancellationToken ct)
    {
        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.OccurrenceId == occurrenceId && a.TelegramUserId == telegramUserId, ct);

        if (existing != null)
        {
            existing.Status = status;
            existing.DancerRole = dancerRole;
            existing.Source = source;
            existing.TelegramUsername = username;
            existing.TelegramDisplayName = displayName;
            existing.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation(
                "[AttendanceTracker] Updated attendance for TgUser={UserId} on Occurrence={OccId}: {Status} ({Role})",
                telegramUserId, occurrenceId, status, dancerRole);
        }
        else
        {
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
                DancerRole = dancerRole,
                Source = source,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "[AttendanceTracker] Created attendance for TgUser={UserId} on Occurrence={OccId}: {Status} ({Role})",
                telegramUserId, occurrenceId, status, dancerRole);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task RemoveAttendanceAsync(int occurrenceId, long telegramUserId, CancellationToken ct)
    {
        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.OccurrenceId == occurrenceId && a.TelegramUserId == telegramUserId, ct);

        if (existing != null)
        {
            // Не удаляем, а помечаем как отозванный — сохраняем историю
            existing.Status = AttendanceStatus.Retracted;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "[AttendanceTracker] Retracted attendance for TgUser={UserId} on Occurrence={OccId}",
                telegramUserId, occurrenceId);
        }
    }

    // ─── Статистика ───────────────────────────────────────────────────────────

    /// <summary>
    /// Возвращает статистику attendance для отображения в боте.
    /// Тренеры (Trainer) не учитываются в общем балансе на занятиях.
    /// </summary>
    public async Task<AttendanceSummary> GetSummaryAsync(int occurrenceId, CancellationToken ct)
    {
        var attendances = await _db.Attendances
            .Where(a => a.OccurrenceId == occurrenceId)
            .ToListAsync(ct);

        var going = attendances.Where(a => a.Status == AttendanceStatus.Going).ToList();

        return new AttendanceSummary
        {
            Total = going.Count,
            NotGoing = attendances.Count(a => a.Status == AttendanceStatus.NotGoing),
            Males = going.Count(a => a.DancerRole == DancerRoleAttendance.Male),
            Females = going.Count(a => a.DancerRole == DancerRoleAttendance.Female),
            Trainers = going.Count(a => a.DancerRole == DancerRoleAttendance.Trainer),
        };
    }
}

public record AttendanceSummary
{
    public int Total { get; init; }
    public int NotGoing { get; init; }
    public int Males { get; init; }
    public int Females { get; init; }
    public int Trainers { get; init; }
}