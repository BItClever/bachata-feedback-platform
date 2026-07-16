using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using BachataFeedback.TelegramBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BachataFeedback.TelegramBot.Handlers;

/// <summary>
/// Команды LLM-аналитики чата:
///   /summary [100|200|300]  — краткое содержание последних N сообщений (доступно всем)
///   /profile @username [N]  — профиль пользователя по его сообщениям (только admin)
///   /analytics [N]          — общая аналитика: активность, темы, тональность (только admin)
/// </summary>
public class AnalyticsCommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly ApplicationDbContext _db;
    private readonly AnalysisJobPublisher _publisher;
    private readonly BotConfiguration _cfg;
    private readonly ILogger<AnalyticsCommandHandler> _logger;

    public AnalyticsCommandHandler(
        ITelegramBotClient bot,
        ApplicationDbContext db,
        AnalysisJobPublisher publisher,
        BotConfiguration cfg,
        ILogger<AnalyticsCommandHandler> logger)
    {
        _bot = bot;
        _db = db;
        _publisher = publisher;
        _cfg = cfg;
        _logger = logger;
    }

    public async Task HandleAsync(Message msg, CancellationToken ct)
    {
        var raw = msg.Text ?? "";
        var spaceIdx = raw.IndexOf(' ');
        var cmd = (spaceIdx > 0 ? raw[..spaceIdx] : raw).Split('@')[0].ToLowerInvariant();
        var args = spaceIdx > 0 ? raw[(spaceIdx + 1)..].Trim() : "";

        switch (cmd)
        {
            case "/summary":
                await HandleSummaryAsync(msg, args, ct);
                break;
            case "/profile":
                await RequireAdminAsync(msg, () => HandleProfileAsync(msg, args, ct), ct);
                break;
            case "/analytics":
                await RequireAdminAsync(msg, () => HandleAnalyticsAsync(msg, args, ct), ct);
                break;
        }
    }

    // ─── /summary [N] ────────────────────────────────────────────────────────

    private async Task HandleSummaryAsync(Message msg, string args, CancellationToken ct)
    {
        var count = ParseCount(args, 100, new[] { 100, 200, 300 });

        // Проверяем, есть ли вообще сообщения
        var available = await _db.ChatMessages
            .CountAsync(cm => cm.TelegramChatId == msg.Chat.Id, ct);

        if (available == 0)
        {
            await _bot.SendMessage(msg.Chat.Id,
                "📭 В этом чате пока нет сохранённых сообщений для анализа.\n"
                + "Убедитесь, что бот является администратором чата.",
                parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        var actualCount = Math.Min(count, available);
        await _bot.SendMessage(msg.Chat.Id,
            $"⏳ Анализирую последние <b>{actualCount}</b> сообщений...\n"
            + "Результат появится через несколько секунд.",
            parseMode: ParseMode.Html, cancellationToken: ct);

        await _publisher.EnqueueAsync(
            AnalysisJobType.Summary,
            chatId: msg.Chat.Id,
            messageCount: actualCount,
            requestedByUserId: msg.From?.Id ?? 0,
            ct: ct);

        _logger.LogInformation("[Analytics] /summary {Count} requested in chat {ChatId}", actualCount, msg.Chat.Id);
    }

    // ─── /profile @username [N] ───────────────────────────────────────────────

    private async Task HandleProfileAsync(Message msg, string args, CancellationToken ct)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            await _bot.SendMessage(msg.Chat.Id,
                "❌ Укажи пользователя: /profile @username [N]\nПример: /profile @john_doe 200",
                cancellationToken: ct);
            return;
        }

        var usernameArg = parts[0].TrimStart('@').ToLowerInvariant();
        var count = parts.Length > 1 ? ParseCount(parts[1], 100, new[] { 100, 200, 300, 500 }) : 100;

        // �?щем пользователя в истории чата
        var userMsg = await _db.ChatMessages
            .Where(cm => cm.TelegramChatId == msg.Chat.Id
                && cm.Username != null
                && cm.Username.ToLower() == usernameArg)
            .OrderByDescending(cm => cm.SentAt)
            .FirstOrDefaultAsync(ct);

        if (userMsg == null)
        {
            await _bot.SendMessage(msg.Chat.Id,
                $"❌ Пользователь @{usernameArg} не найден в истории этого чата.",
                cancellationToken: ct);
            return;
        }

        var displayName = userMsg.FirstName ?? $"@{usernameArg}";
        var msgCount = await _db.ChatMessages.CountAsync(
            cm => cm.TelegramChatId == msg.Chat.Id && cm.TelegramUserId == userMsg.TelegramUserId, ct);

        if (msgCount == 0)
        {
            await _bot.SendMessage(msg.Chat.Id,
                $"📭 У @{usernameArg} нет сохранённых сообщений в этом чате.",
                cancellationToken: ct);
            return;
        }

        var actualCount = Math.Min(count, msgCount);
        await _bot.SendMessage(msg.Chat.Id,
            $"⏳ Анализирую профиль <b>{displayName}</b> по {actualCount} сообщениям...",
            parseMode: ParseMode.Html, cancellationToken: ct);

        await _publisher.EnqueueAsync(
            AnalysisJobType.UserProfile,
            chatId: msg.Chat.Id,
            messageCount: actualCount,
            requestedByUserId: msg.From?.Id ?? 0,
            targetUserId: userMsg.TelegramUserId,
            targetUserDisplayName: displayName,
            ct: ct);
    }

    // ─── /analytics [N] ───────────────────────────────────────────────────────

    private async Task HandleAnalyticsAsync(Message msg, string args, CancellationToken ct)
    {
        var count = ParseCount(args, 200, new[] { 100, 200, 300 });

        var available = await _db.ChatMessages
            .CountAsync(cm => cm.TelegramChatId == msg.Chat.Id, ct);

        if (available == 0)
        {
            await _bot.SendMessage(msg.Chat.Id,
                "📭 В этом чате пока нет сохранённых сообщений.",
                cancellationToken: ct);
            return;
        }

        var actualCount = Math.Min(count, available);

        // Статистика без LLM (быстро)
        var stats = await BuildQuickStatsAsync(msg.Chat.Id, actualCount, ct);
        await _bot.SendMessage(msg.Chat.Id, stats, parseMode: ParseMode.Html, cancellationToken: ct);

        // LLM-анализ (асинхронно)
        await _bot.SendMessage(msg.Chat.Id,
            $"⏳ Запускаю LLM-анализ последних <b>{actualCount}</b> сообщений...",
            parseMode: ParseMode.Html, cancellationToken: ct);

        await _publisher.EnqueueAsync(
            AnalysisJobType.ChatAnalytics,
            chatId: msg.Chat.Id,
            messageCount: actualCount,
            requestedByUserId: msg.From?.Id ?? 0,
            ct: ct);
    }

    // ─── HELPERS ─────────────────────────────────────────────────────────────

    private async Task<string> BuildQuickStatsAsync(long chatId, int count, CancellationToken ct)
    {
        var messages = await _db.ChatMessages
            .Where(cm => cm.TelegramChatId == chatId && cm.Text != null && !cm.IsBot)
            .OrderByDescending(cm => cm.SentAt)
            .Take(count)
            .ToListAsync(ct);

        var topUsers = messages
            .Where(m => m.TelegramUserId != null)
            .GroupBy(m => m.TelegramUserId)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g =>
            {
                var first = g.First();
                var name = first.Username != null ? $"@{first.Username}" : (first.FirstName ?? "Unknown");
                return $"• {name}: {g.Count()} сообщений";
            });

        var totalUsers = messages.Select(m => m.TelegramUserId).Distinct().Count();
        var from = messages.LastOrDefault()?.SentAt;
        var to = messages.FirstOrDefault()?.SentAt;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>📊 Быстрая статистика чата</b> (последние {messages.Count} сообщений)");
        if (from.HasValue && to.HasValue)
            sb.AppendLine($"📅 Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}");
        sb.AppendLine($"👥 Участников: {totalUsers}");
        sb.AppendLine($"\n<b>🏆 Самые активные:</b>");
        foreach (var u in topUsers) sb.AppendLine(u);

        return sb.ToString().TrimEnd();
    }

    private static int ParseCount(string s, int defaultVal, int[] allowed)
    {
        if (int.TryParse(s.Split(' ')[0], out var n))
            return allowed.OrderBy(a => Math.Abs(a - n)).First();
        return defaultVal;
    }

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
}
