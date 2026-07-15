using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using BachataFeedback.TelegramBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace BachataFeedback.TelegramBot.Handlers;

/// <summary>
/// Обрабатывает нажатия inline-кнопок (callback_query).
/// 
/// Форматы callback_data:
///   join:{occurrenceId}       — "Хочу прийти" из зеркала
///   notgoing:{occurrenceId}   — "Не приду" из зеркала/лички
///   info:{occurrenceId}       — "Подробнее" — показываем карточку
/// </summary>
public class CallbackQueryHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly ApplicationDbContext _db;
    private readonly AttendanceTracker _tracker;
    private readonly ILogger<CallbackQueryHandler> _logger;

    public CallbackQueryHandler(
        ITelegramBotClient bot,
        ApplicationDbContext db,
        AttendanceTracker tracker,
        ILogger<CallbackQueryHandler> logger)
    {
        _bot = bot;
        _db = db;
        _tracker = tracker;
        _logger = logger;
    }

    public async Task HandleAsync(CallbackQuery cbq, CancellationToken ct)
    {
        var data = cbq.Data ?? "";
        var userId = cbq.From.Id;
        var username = cbq.From.Username;
        var displayName = (cbq.From.FirstName + " " + cbq.From.LastName).Trim();

        _logger.LogInformation("[Callback] User={UserId} data={Data}", userId, data);

        if (data.StartsWith("join:") && int.TryParse(data[5..], out var joinId))
        {
            await HandleJoinAsync(cbq, joinId, userId, username, displayName, ct);
        }
        else if (data.StartsWith("notgoing:") && int.TryParse(data[9..], out var ngId))
        {
            await HandleNotGoingAsync(cbq, ngId, userId, username, displayName, ct);
        }
        else if (data.StartsWith("info:") && int.TryParse(data[5..], out var infoId))
        {
            await HandleInfoAsync(cbq, infoId, ct);
        }
        else
        {
            await _bot.AnswerCallbackQueryAsync(cbq.Id, "Неизвестная команда", cancellationToken: ct);
        }
    }

    private async Task HandleJoinAsync(
        CallbackQuery cbq, int occurrenceId,
        long userId, string? username, string displayName,
        CancellationToken ct)
    {
        var occurrence = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .FirstOrDefaultAsync(o => o.Id == occurrenceId, ct);

        if (occurrence == null)
        {
            await _bot.AnswerCallbackQueryAsync(cbq.Id, "Занятие не найдено", cancellationToken: ct);
            return;
        }

        if (occurrence.Status == OccurrenceStatus.Cancelled)
        {
            await _bot.AnswerCallbackQueryAsync(cbq.Id, "Это занятие отменено", cancellationToken: ct);
            return;
        }

        // Отвечаем на callback немедленно, чтобы убрать спиннер
        await _bot.AnswerCallbackQueryAsync(cbq.Id, cancellationToken: ct);

        // Отправляем карточку в личку с кнопками подтверждения
        var text = BuildOccurrenceCard(occurrence);
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Записаться", $"confirm:{occurrenceId}"),
                InlineKeyboardButton.WithCallbackData("❌ Не приду", $"notgoing:{occurrenceId}")
            }
        });

        try
        {
            await _bot.SendTextMessageAsync(
                chatId: cbq.From.Id,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Callback] Cannot send DM to user {UserId} — bot may be blocked", userId);
            // Показываем inline-уведомление если личка заблокирована
            await _bot.AnswerCallbackQueryAsync(
                cbq.Id,
                "Пожалуйста, напишите боту в личку, чтобы записаться",
                showAlert: true,
                cancellationToken: ct);
        }
    }

    private async Task HandleNotGoingAsync(
        CallbackQuery cbq, int occurrenceId,
        long userId, string? username, string displayName,
        CancellationToken ct)
    {
        await _tracker.TrackButtonActionAsync(
            occurrenceId, userId, username, displayName,
            AttendanceStatus.NotGoing, ct);

        await _bot.AnswerCallbackQueryAsync(cbq.Id, "Понял, ты не придёшь", cancellationToken: ct);
    }

    private async Task HandleInfoAsync(CallbackQuery cbq, int occurrenceId, CancellationToken ct)
    {
        var occurrence = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .FirstOrDefaultAsync(o => o.Id == occurrenceId, ct);

        if (occurrence == null)
        {
            await _bot.AnswerCallbackQueryAsync(cbq.Id, "Занятие не найдено", cancellationToken: ct);
            return;
        }

        var summary = await _tracker.GetSummaryAsync(occurrenceId, ct);
        var text = BuildOccurrenceCard(occurrence, summary);

        await _bot.AnswerCallbackQueryAsync(cbq.Id, cancellationToken: ct);
        await _bot.SendTextMessageAsync(
            chatId: cbq.From.Id,
            text: text,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    // Обработка подтверждения записи через личку (confirm:id)
    // Регистрируется через join → confirm
    private static string BuildOccurrenceCard(Occurrence o, AttendanceSummary? summary = null)
    {
        var timeStr = o.StartsAt.ToString("dd.MM.yyyy HH:mm");
        var type = o.Type switch
        {
            OccurrenceType.Lesson => "Занятие",
            OccurrenceType.Party => "🎉 Вечеринка",
            OccurrenceType.Trip => "✈️ Поездка",
            OccurrenceType.Practice => "🕺 Практика",
            _ => o.Type
        };

        var lines = new List<string>
        {
            $"<b>{type}</b>",
            $"📅 {timeStr}"
        };

        if (o.DanceGroup != null)
            lines.Add($"👥 Группа: {o.DanceGroup.Name}");
        if (!string.IsNullOrEmpty(o.Level))
            lines.Add($"📊 Уровень: {o.Level}");
        if (o.Capacity.HasValue)
            lines.Add($"🎯 Лимит: {o.Capacity} чел.");
        if (!string.IsNullOrEmpty(o.Title))
            lines.Add($"📌 {o.Title}");
        if (!string.IsNullOrEmpty(o.Notes))
            lines.Add($"\n{o.Notes}");

        if (summary != null)
        {
            lines.Add("");
            lines.Add($"👤 Записалось: <b>{summary.Total}</b>");
            if (summary.Leads > 0 || summary.Follows > 0)
                lines.Add($"🕺 Лиды: {summary.Leads} | 💃 Фолловеры: {summary.Follows}");
        }

        return string.Join("\n", lines);
    }
}
