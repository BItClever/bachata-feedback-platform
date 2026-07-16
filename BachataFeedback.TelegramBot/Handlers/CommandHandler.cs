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
/// Обрабатывает slash-команды.
///
/// Публичные (любой чат):
///   /next_event  — ближайший ивент
///   /today       — занятия сегодня
///   /support     — дисбаланс лидов/фолловеров
///   /start       — приветствие
///
/// Только для admin:
///   /add_lesson yyyy-MM-dd HH:mm [Уровень] [лимит]  — создать занятие
///   /add_party  yyyy-MM-dd HH:mm [Название] [лимит]  — создать вечеринку
///   /publish {id}        — опубликовать occurrence (canonical poll в primary-чат + зеркала)
///   /close_poll {id}     — закрыть poll
///   /occurrences         — список предстоящих
///   /cancel {id}         — отменить занятие/ивент
/// </summary>
public class CommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly ApplicationDbContext _db;
    private readonly OccurrencePublisher _publisher;
    private readonly BotConfiguration _cfg;
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(
        ITelegramBotClient bot,
        ApplicationDbContext db,
        OccurrencePublisher publisher,
        BotConfiguration cfg,
        ILogger<CommandHandler> logger)
    {
        _bot = bot;
        _db = db;
        _publisher = publisher;
        _cfg = cfg;
        _logger = logger;
    }

    public async Task HandleAsync(Message msg, CancellationToken ct)
    {
        var text = msg.Text ?? "";
        // Убираем @botname суффикс в группах
        var spaceIdx = text.IndexOf(' ');
        var cmd = (spaceIdx > 0 ? text[..spaceIdx] : text).Split('@')[0].ToLowerInvariant();
        var args = spaceIdx > 0 ? text[(spaceIdx + 1)..].Trim() : "";

        _logger.LogInformation("[Command] Chat={ChatId} User={UserId} cmd={Cmd} args={Args}",
            msg.Chat.Id, msg.From?.Id, cmd, args);

        switch (cmd)
        {
            case "/start":
                await HandleStartAsync(msg, ct);
                break;
            case "/next_event":
                await HandleNextEventAsync(msg, ct);
                break;
            case "/today":
                await HandleTodayAsync(msg, ct);
                break;
            case "/support":
                await HandleSupportAsync(msg, ct);
                break;

            // Admin команды
            case "/add_lesson":
                await RequireAdminAsync(msg, () => HandleAddLessonAsync(msg, args, ct), ct);
                break;
            case "/add_party":
                await RequireAdminAsync(msg, () => HandleAddPartyAsync(msg, args, ct), ct);
                break;
            case "/publish":
                await RequireAdminAsync(msg, () => HandlePublishAsync(msg, args, ct), ct);
                break;
            case "/close_poll":
                await RequireAdminAsync(msg, () => HandleClosePollAsync(msg, args, ct), ct);
                break;
            case "/occurrences":
                await RequireAdminAsync(msg, () => HandleListOccurrencesAsync(msg, ct), ct);
                break;
            case "/cancel":
                await RequireAdminAsync(msg, () => HandleCancelAsync(msg, args, ct), ct);
                break;

            default:
                // Незнакомые команды игнорируем (в группах это нормально)
                break;
        }
    }

    // ─── PUBLIC COMMANDS ──────────────────────────────────────────────────────

    private async Task HandleStartAsync(Message msg, CancellationToken ct)
    {
        var text = "<b>👋 Привет! Я бот школы bachata.</b>\n\n"
            + "Я умею:\n"
            + "• /next_event — ближайший ивент\n"
            + "• /today — занятия сегодня\n"
            + "• /support — где нужен саппорт\n\n"
            + "Или набери <code>@botname</code> в любом чате для inline-поиска.";

        await _bot.SendMessage(msg.Chat.Id, text, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private async Task HandleNextEventAsync(Message msg, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var next = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .Include(o => o.Attendances)
            .Where(o => o.StartsAt >= now && o.Status == OccurrenceStatus.Published)
            .OrderBy(o => o.StartsAt)
            .FirstOrDefaultAsync(ct);

        if (next == null)
        {
            await _bot.SendMessage(msg.Chat.Id, "😔 Ближайших занятий/ивентов пока нет.", cancellationToken: ct);
            return;
        }

        var goingCount = next.Attendances.Count(a => a.Status == AttendanceStatus.Going);
        var text = BuildOccurrenceText(next, goingCount);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Хочу прийти", $"join:{next.Id}"),
                InlineKeyboardButton.WithCallbackData("ℹ️ Подробнее", $"info:{next.Id}")
            }
        });

        await _bot.SendMessage(msg.Chat.Id, text, parseMode: ParseMode.Html, replyMarkup: keyboard, cancellationToken: ct);
    }

    private async Task HandleTodayAsync(Message msg, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var occurrences = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .Include(o => o.Attendances)
            .Where(o => o.StartsAt >= today && o.StartsAt < tomorrow && o.Status == OccurrenceStatus.Published)
            .OrderBy(o => o.StartsAt)
            .ToListAsync(ct);

        if (!occurrences.Any())
        {
            await _bot.SendMessage(msg.Chat.Id, "Сегодня занятий нет 😌", cancellationToken: ct);
            return;
        }

        foreach (var o in occurrences)
        {
            var goingCount = o.Attendances.Count(a => a.Status == AttendanceStatus.Going);
            var text = BuildOccurrenceText(o, goingCount);
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ Хочу прийти", $"join:{o.Id}"),
                    InlineKeyboardButton.WithCallbackData("ℹ️ Подробнее", $"info:{o.Id}")
                }
            });
            await _bot.SendMessage(msg.Chat.Id, text, parseMode: ParseMode.Html, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    private async Task HandleSupportAsync(Message msg, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var soon = now.AddHours(48);

        var occurrences = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .Include(o => o.Attendances)
            .Where(o => o.StartsAt >= now && o.StartsAt <= soon
                && o.Status == OccurrenceStatus.Published
                && (o.BalanceLeads != null || o.BalanceFollows != null))
            .ToListAsync(ct);

        var needSupport = occurrences.Where(o =>
        {
            var going = o.Attendances.Where(a => a.Status == AttendanceStatus.Going).ToList();
            var leads = going.Count(a => a.DancerRole == "lead");
            var follows = going.Count(a => a.DancerRole == "follow");
            return (o.BalanceLeads.HasValue && leads < o.BalanceLeads.Value)
                || (o.BalanceFollows.HasValue && follows < o.BalanceFollows.Value);
        }).ToList();

        if (!needSupport.Any())
        {
            await _bot.SendMessage(msg.Chat.Id, "✅ На ближайших занятиях (48ч) баланс в норме.", cancellationToken: ct);
            return;
        }

        foreach (var o in needSupport)
        {
            var going = o.Attendances.Where(a => a.Status == AttendanceStatus.Going).ToList();
            var leads = going.Count(a => a.DancerRole == "lead");
            var follows = going.Count(a => a.DancerRole == "follow");
            var timeStr = o.StartsAt.ToString("dd.MM HH:mm");

            var text = $"<b>🤝 Нужен саппорт</b>\n"
                + $"📅 {timeStr} — {o.DanceGroup?.Name ?? o.Type}\n"
                + $"🕺 Лиды: {leads}/{o.BalanceLeads} | 💃 Фолловеры: {follows}/{o.BalanceFollows}";

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🤝 Я могу помочь", $"join:{o.Id}") }
            });

            await _bot.SendMessage(msg.Chat.Id, text, parseMode: ParseMode.Html, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    // ─── ADMIN COMMANDS ───────────────────────────────────────────────────────

    /// <summary>
    /// /add_lesson 2026-07-20 19:00 Intermediate 16
    /// </summary>
    private async Task HandleAddLessonAsync(Message msg, string args, CancellationToken ct)
    {
        // Формат: yyyy-MM-dd HH:mm [Level] [Capacity]
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await _bot.SendMessage(msg.Chat.Id,
                "❌ Формат: /add_lesson 2026-07-20 19:00 [Уровень] [лимит]\nПример: /add_lesson 2026-07-20 19:00 Intermediate 16",
                cancellationToken: ct);
            return;
        }

        if (!DateTime.TryParse($"{parts[0]} {parts[1]}", out var startsAt))
        {
            await _bot.SendMessage(msg.Chat.Id, "❌ Неверный формат даты. Используй: yyyy-MM-dd HH:mm", cancellationToken: ct);
            return;
        }

        var level = parts.Length > 2 ? parts[2] : null;
        var capacity = parts.Length > 3 && int.TryParse(parts[3], out var cap) ? cap : (int?)null;

        var occurrence = new Occurrence
        {
            Type = OccurrenceType.Lesson,
            StartsAt = DateTime.SpecifyKind(startsAt, DateTimeKind.Utc),
            Level = level,
            Capacity = capacity,
            Status = OccurrenceStatus.Draft
        };

        _db.Occurrences.Add(occurrence);
        await _db.SaveChangesAsync(ct);

        await _bot.SendMessage(msg.Chat.Id,
            $"✅ Занятие создано (ID: <b>{occurrence.Id}</b>)\n"
            + $"📅 {startsAt:dd.MM.yyyy HH:mm}\n"
            + (level != null ? $"📊 {level}\n" : "")
            + (capacity.HasValue ? $"🎯 Лимит: {capacity}\n" : "")
            + $"\nДля публикации: /publish {occurrence.Id}",
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    /// <summary>
    /// /add_party 2026-07-25 21:00 Вечеринка на набережной 50
    /// </summary>
    private async Task HandleAddPartyAsync(Message msg, string args, CancellationToken ct)
    {
        var parts = args.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await _bot.SendMessage(msg.Chat.Id,
                "❌ Формат: /add_party 2026-07-25 21:00 [Название] [лимит]",
                cancellationToken: ct);
            return;
        }

        if (!DateTime.TryParse($"{parts[0]} {parts[1]}", out var startsAt))
        {
            await _bot.SendMessage(msg.Chat.Id, "❌ Неверный формат даты.", cancellationToken: ct);
            return;
        }

        string? title = null;
        int? capacity = null;

        if (parts.Length > 2)
        {
            // Последний токен — число? Это capacity
            var last = parts[^1];
            if (int.TryParse(last, out var cap))
            {
                capacity = cap;
                title = parts.Length > 3 ? parts[2] : null;
            }
            else
            {
                title = parts[2];
            }
        }

        var occurrence = new Occurrence
        {
            Type = OccurrenceType.Party,
            Title = title,
            StartsAt = DateTime.SpecifyKind(startsAt, DateTimeKind.Utc),
            Capacity = capacity,
            Status = OccurrenceStatus.Draft
        };

        _db.Occurrences.Add(occurrence);
        await _db.SaveChangesAsync(ct);

        await _bot.SendMessage(msg.Chat.Id,
            $"✅ Вечеринка создана (ID: <b>{occurrence.Id}</b>)\n"
            + $"📅 {startsAt:dd.MM.yyyy HH:mm}\n"
            + (title != null ? $"📌 {title}\n" : "")
            + (capacity.HasValue ? $"🎯 Лимит: {capacity}\n" : "")
            + $"\nДля публикации: /publish {occurrence.Id}",
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    /// <summary>
    /// /publish {id} — публикует occurrence: canonical poll в primary-чат, зеркала в остальные
    /// </summary>
    private async Task HandlePublishAsync(Message msg, string args, CancellationToken ct)
    {
        if (!int.TryParse(args.Trim(), out var id))
        {
            await _bot.SendMessage(msg.Chat.Id, "❌ Укажи ID: /publish 42", cancellationToken: ct);
            return;
        }

        var occurrence = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (occurrence == null)
        {
            await _bot.SendMessage(msg.Chat.Id, $"❌ Occurrence #{id} не найден", cancellationToken: ct);
            return;
        }

        if (occurrence.Status == OccurrenceStatus.Published)
        {
            await _bot.SendMessage(msg.Chat.Id, $"⚠️ Occurrence #{id} уже опубликован", cancellationToken: ct);
            return;
        }

        try
        {
            await _publisher.PublishAsync(occurrence, ct);
            await _bot.SendMessage(msg.Chat.Id,
                $"✅ Occurrence #{id} опубликован во все настроенные чаты",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommandHandler] Publish failed for occurrence #{Id}", id);
            await _bot.SendMessage(msg.Chat.Id,
                $"❌ Ошибка публикации: {ex.Message}",
                cancellationToken: ct);
        }
    }

    /// <summary>
    /// /close_poll {id} — закрывает poll и финализирует статус occurrence
    /// </summary>
    private async Task HandleClosePollAsync(Message msg, string args, CancellationToken ct)
    {
        if (!int.TryParse(args.Trim(), out var id))
        {
            await _bot.SendMessage(msg.Chat.Id, "❌ Укажи ID: /close_poll 42", cancellationToken: ct);
            return;
        }

        var publication = await _db.OccurrencePublications
            .Include(op => op.Occurrence)
            .FirstOrDefaultAsync(op => op.OccurrenceId == id && op.IsVotingSource, ct);

        if (publication == null)
        {
            await _bot.SendMessage(msg.Chat.Id,
                $"❌ Canonical poll для Occurrence #{id} не найден",
                cancellationToken: ct);
            return;
        }

        if (string.IsNullOrEmpty(publication.TelegramPollId) || !publication.TelegramMessageId.HasValue)
        {
            await _bot.SendMessage(msg.Chat.Id, "❌ Poll ещё не опубликован", cancellationToken: ct);
            return;
        }

        try
        {
            await _bot.StopPoll(
                chatId: publication.TelegramChatId,
                messageId: (int)publication.TelegramMessageId.Value,
                cancellationToken: ct);

            publication.Occurrence.Status = OccurrenceStatus.Completed;
            await _db.SaveChangesAsync(ct);

            await _bot.SendMessage(msg.Chat.Id,
                $"✅ Poll для Occurrence #{id} закрыт. Статус: completed.",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommandHandler] StopPoll failed for occurrence #{Id}", id);
            await _bot.SendMessage(msg.Chat.Id, $"❌ Ошибка: {ex.Message}", cancellationToken: ct);
        }
    }

    /// <summary>
    /// /occurrences — список предстоящих занятий/ивентов
    /// </summary>
    private async Task HandleListOccurrencesAsync(Message msg, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var upcoming = await _db.Occurrences
            .Include(o => o.DanceGroup)
            .Include(o => o.Attendances)
            .Where(o => o.StartsAt >= now)
            .OrderBy(o => o.StartsAt)
            .Take(15)
            .ToListAsync(ct);

        if (!upcoming.Any())
        {
            await _bot.SendMessage(msg.Chat.Id, "Нет запланированных занятий.", cancellationToken: ct);
            return;
        }

        var lines = new List<string> { "<b>📋 Предстоящие занятия/ивенты:</b>\n" };
        foreach (var o in upcoming)
        {
            var goingCount = o.Attendances.Count(a => a.Status == AttendanceStatus.Going);
            var statusEmoji = o.Status switch
            {
                OccurrenceStatus.Draft => "📝",
                OccurrenceStatus.Published => "✅",
                OccurrenceStatus.Cancelled => "❌",
                OccurrenceStatus.Completed => "🏁",
                _ => "❓"
            };
            var typeEmoji = o.Type == OccurrenceType.Party ? "🎉" : "📚";
            lines.Add($"{statusEmoji} <b>#{o.Id}</b> {typeEmoji} {o.StartsAt:dd.MM HH:mm} "
                + $"— {o.DanceGroup?.Name ?? o.Title ?? o.Type} "
                + $"[{o.Level ?? "-"}] 👤{goingCount}");
        }

        await _bot.SendMessage(msg.Chat.Id, string.Join("\n", lines), parseMode: ParseMode.Html, cancellationToken: ct);
    }

    /// <summary>
    /// /cancel {id} — отменяет занятие
    /// </summary>
    private async Task HandleCancelAsync(Message msg, string args, CancellationToken ct)
    {
        if (!int.TryParse(args.Trim(), out var id))
        {
            await _bot.SendMessage(msg.Chat.Id, "❌ Укажи ID: /cancel 42", cancellationToken: ct);
            return;
        }

        var occurrence = await _db.Occurrences.FindAsync(new object[] { id }, ct);
        if (occurrence == null)
        {
            await _bot.SendMessage(msg.Chat.Id, $"❌ Occurrence #{id} не найден", cancellationToken: ct);
            return;
        }

        occurrence.Status = OccurrenceStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        await _bot.SendMessage(msg.Chat.Id,
            $"✅ Occurrence #{id} отменён.",
            cancellationToken: ct);
    }

    // ─── HELPERS ─────────────────────────────────────────────────────────────

    private async Task RequireAdminAsync(Message msg, Func<Task> action, CancellationToken ct)
    {
        var userId = msg.From?.Id ?? 0;
        if (!_cfg.IsAdmin(userId))
        {
            await _bot.SendMessage(msg.Chat.Id,
                "⛔ Эта команда доступна только администраторам.",
                cancellationToken: ct);
            return;
        }
        await action();
    }

    private static string BuildOccurrenceText(Occurrence o, int goingCount)
    {
        var timeStr = o.StartsAt.ToString("dd.MM.yyyy HH:mm");
        var typeLabel = o.Type switch
        {
            OccurrenceType.Lesson => "📚 Занятие",
            OccurrenceType.Party => "🎉 Вечеринка",
            OccurrenceType.Trip => "✈️ Поездка",
            OccurrenceType.Practice => "🕺 Практика",
            _ => o.Type
        };

        var lines = new List<string> { $"<b>{typeLabel}</b>", $"📅 {timeStr}" };
        if (o.DanceGroup != null) lines.Add($"👥 {o.DanceGroup.Name}");
        if (!string.IsNullOrEmpty(o.Level)) lines.Add($"📊 {o.Level}");
        if (!string.IsNullOrEmpty(o.Title)) lines.Add($"📌 {o.Title}");
        if (!string.IsNullOrEmpty(o.Notes)) lines.Add($"\n{o.Notes}");
        lines.Add($"\n👤 Записалось: <b>{goingCount}</b>" + (o.Capacity.HasValue ? $" / {o.Capacity}" : ""));

        return string.Join("\n", lines);
    }
}
