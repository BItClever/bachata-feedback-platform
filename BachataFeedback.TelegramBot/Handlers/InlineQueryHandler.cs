using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace BachataFeedback.TelegramBot.Handlers;

/// <summary>
/// Обрабатывает inline-запросы (@bot запрос).
/// Поддерживаемые запросы:
///   "сегодня" / "today"   — занятия сегодня
///   "вечеринки" / "party" — ближайшие вечеринки
///   "саппорт" / "support" — где нужен саппорт
///   (пусто)               — ближайшие ивенты
/// </summary>
public class InlineQueryHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<InlineQueryHandler> _logger;

    public InlineQueryHandler(ITelegramBotClient bot, ApplicationDbContext db, ILogger<InlineQueryHandler> logger)
    {
        _bot = bot;
        _db = db;
        _logger = logger;
    }

    public async Task HandleAsync(InlineQuery iq, CancellationToken ct)
    {
        var query = (iq.Query ?? "").Trim().ToLowerInvariant();
        _logger.LogInformation("[InlineQuery] User={UserId} query='{Query}'", iq.From.Id, query);

        var results = new List<InlineQueryResult>();

        if (query.Contains("сегодня") || query.Contains("today"))
        {
            results = await BuildTodayResultsAsync(ct);
        }
        else if (query.Contains("вечеринк") || query.Contains("party"))
        {
            results = await BuildPartyResultsAsync(ct);
        }
        else if (query.Contains("саппорт") || query.Contains("support"))
        {
            results = await BuildSupportResultsAsync(ct);
        }
        else
        {
            // По умолчанию — ближайшие занятия и ивенты
            results = await BuildUpcomingResultsAsync(ct);
        }

        await _bot.AnswerInlineQuery(
            iq.Id,
            results,
            cacheTime: 30,
            cancellationToken: ct);
    }

    private async Task<List<InlineQueryResult>> BuildTodayResultsAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var occurrences = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .Include(o => o.Attendances)
            .Where(o => o.StartsAt >= today && o.StartsAt < tomorrow
                && o.Status == OccurrenceStatus.Published)
            .OrderBy(o => o.StartsAt)
            .Take(10)
            .ToListAsync(ct);

        if (!occurrences.Any())
        {
            return [BuildEmptyResult("today", "Сегодня нет занятий 🤷", "Занятий на сегодня не запланировано")];
        }

        return occurrences.Select((o, i) => BuildOccurrenceResult(i.ToString(), o)).ToList<InlineQueryResult>();
    }

    private async Task<List<InlineQueryResult>> BuildPartyResultsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var parties = await _db.Occurrences
            .Include(o => o.Attendances)
            .Where(o => o.Type == OccurrenceType.Party && o.StartsAt >= now
                && o.Status == OccurrenceStatus.Published)
            .OrderBy(o => o.StartsAt)
            .Take(5)
            .ToListAsync(ct);

        if (!parties.Any())
        {
            return [BuildEmptyResult("party", "Вечеринок не запланировано 🎭", "Ближайших вечеринок пока нет")];
        }

        return parties.Select((o, i) => BuildOccurrenceResult(i.ToString(), o)).ToList<InlineQueryResult>();
    }

    private async Task<List<InlineQueryResult>> BuildSupportResultsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var soon = now.AddHours(24);

        // Занятия, где нужен баланс (есть BalanceLeads/BalanceFollows и реальный дисбаланс)
        var occurrences = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .Include(o => o.Attendances)
            .Where(o => o.StartsAt >= now && o.StartsAt <= soon
                && o.Status == OccurrenceStatus.Published
                && (o.BalanceLeads != null || o.BalanceFollows != null))
            .ToListAsync(ct);

        var needSupport = occurrences
            .Where(o =>
            {
                var going = o.Attendances.Where(a => a.Status == AttendanceStatus.Going).ToList();
                var leads = going.Count(a => a.DancerRole == "lead");
                var follows = going.Count(a => a.DancerRole == "follow");
                return (o.BalanceLeads.HasValue && leads < o.BalanceLeads.Value)
                    || (o.BalanceFollows.HasValue && follows < o.BalanceFollows.Value);
            })
            .ToList();

        if (!needSupport.Any())
        {
            return [BuildEmptyResult("support", "Саппорт не нужен ✅", "На ближайших занятиях баланс соблюдён")];
        }

        return needSupport.Select((o, i) => BuildSupportResult(i.ToString(), o)).ToList<InlineQueryResult>();
    }

    private async Task<List<InlineQueryResult>> BuildUpcomingResultsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var occurrences = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .Include(o => o.Attendances)
            .Where(o => o.StartsAt >= now && o.Status == OccurrenceStatus.Published)
            .OrderBy(o => o.StartsAt)
            .Take(8)
            .ToListAsync(ct);

        if (!occurrences.Any())
        {
            return [BuildEmptyResult("empty", "Нет ближайших занятий", "Занятия ещё не запланированы")];
        }

        return occurrences.Select((o, i) => BuildOccurrenceResult(i.ToString(), o)).ToList<InlineQueryResult>();
    }

    private static InlineQueryResultArticle BuildOccurrenceResult(string id, Occurrence o)
    {
        var goingCount = o.Attendances.Count(a => a.Status == AttendanceStatus.Going);
        var timeStr = o.StartsAt.ToString("dd.MM HH:mm");
        var typeEmoji = o.Type switch
        {
            OccurrenceType.Lesson => "📚",
            OccurrenceType.Party => "🎉",
            OccurrenceType.Trip => "✈️",
            OccurrenceType.Practice => "🕺",
            _ => "📌"
        };

        var title = string.IsNullOrEmpty(o.Title)
            ? $"{typeEmoji} {o.DanceGroup?.Name ?? o.Type} — {timeStr}"
            : $"{typeEmoji} {o.Title} — {timeStr}";

        var description = $"Записалось: {goingCount}"
            + (o.Capacity.HasValue ? $" / {o.Capacity}" : "")
            + (string.IsNullOrEmpty(o.Level) ? "" : $" | {o.Level}");

        var messageText = BuildMessageText(o, goingCount);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Хочу прийти", $"join:{o.Id}"),
                InlineKeyboardButton.WithCallbackData("ℹ️ Подробнее", $"info:{o.Id}")
            }
        });

        return new InlineQueryResultArticle(id, title, new InputTextMessageContent(messageText)
        {
            ParseMode = Telegram.Bot.Types.Enums.ParseMode.Html
        })
        {
            Description = description,
            ReplyMarkup = keyboard
        };
    }

    private static InlineQueryResultArticle BuildSupportResult(string id, Occurrence o)
    {
        var going = o.Attendances.Where(a => a.Status == AttendanceStatus.Going).ToList();
        var leads = going.Count(a => a.DancerRole == "lead");
        var follows = going.Count(a => a.DancerRole == "follow");
        var timeStr = o.StartsAt.ToString("dd.MM HH:mm");

        var title = $"🤝 Нужен саппорт — {o.DanceGroup?.Name ?? o.Type} {timeStr}";
        var description = $"Лиды: {leads}/{o.BalanceLeads} | Фолловеры: {follows}/{o.BalanceFollows}";

        var messageText = $"<b>🤝 Нужен саппорт!</b>\n"
            + $"📅 {timeStr}\n"
            + $"👥 {o.DanceGroup?.Name ?? o.Type}\n"
            + $"🕺 Лиды: {leads}/{o.BalanceLeads} | 💃 Фолловеры: {follows}/{o.BalanceFollows}\n\n"
            + "Если можешь помочь — нажми кнопку ниже 👇";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🤝 Я могу помочь", $"join:{o.Id}")
            }
        });

        return new InlineQueryResultArticle(id, title, new InputTextMessageContent(messageText)
        {
            ParseMode = Telegram.Bot.Types.Enums.ParseMode.Html
        })
        {
            Description = description,
            ReplyMarkup = keyboard
        };
    }

    private static InlineQueryResultArticle BuildEmptyResult(string id, string title, string description)
    {
        return new InlineQueryResultArticle(id, title,
            new InputTextMessageContent(description));
    }

    private static string BuildMessageText(Occurrence o, int goingCount)
    {
        var timeStr = o.StartsAt.ToString("dd.MM.yyyy HH:mm");
        var typeLabel = o.Type switch
        {
            OccurrenceType.Lesson => "Занятие",
            OccurrenceType.Party => "🎉 Вечеринка",
            OccurrenceType.Trip => "✈️ Поездка",
            OccurrenceType.Practice => "🕺 Практика",
            _ => o.Type
        };

        var lines = new List<string> { $"<b>{typeLabel}</b>", $"📅 {timeStr}" };
        if (o.DanceGroup != null) lines.Add($"👥 {o.DanceGroup.Name}");
        if (!string.IsNullOrEmpty(o.Level)) lines.Add($"📊 {o.Level}");
        if (!string.IsNullOrEmpty(o.Title)) lines.Add($"📌 {o.Title}");
        lines.Add($"\n👤 Записалось: <b>{goingCount}</b>" + (o.Capacity.HasValue ? $" / {o.Capacity}" : ""));

        return string.Join("\n", lines);
    }
}
