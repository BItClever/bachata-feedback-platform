using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace BachataFeedback.TelegramBot.Services;

/// <summary>
/// Публикует Occurrence в настроенные Telegram-чаты.
///
/// Логика публикации:
/// 1. Определяем canonical-чат (group_primary для занятия, events_chat для вечеринки).
///    В нём отправляем неанонимный poll — это voting source.
/// 2. В остальные активные чаты (all_lessons_feed, flood_chat) — зеркало:
///    текстовое сообщение с inline-кнопками "Хочу прийти" и "Подробнее".
/// 3. Обновляем статус Occurrence → Published.
/// </summary>
public class OccurrencePublisher
{
    private readonly ITelegramBotClient _bot;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OccurrencePublisher> _logger;

    public OccurrencePublisher(ITelegramBotClient bot, ApplicationDbContext db, ILogger<OccurrencePublisher> logger)
    {
        _bot = bot;
        _db = db;
        _logger = logger;
    }

    public async Task PublishAsync(Occurrence occurrence, CancellationToken ct)
    {
        var activeChats = await _db.TelegramChats
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        if (!activeChats.Any())
        {
            throw new InvalidOperationException("Нет активных чатов для публикации. Добавьте чаты через API /api/telegram-chats.");
        }

        // Определяем canonical-чат
        TelegramChat? canonicalChat = FindCanonicalChat(occurrence, activeChats);

        if (canonicalChat != null)
        {
            await PublishCanonicalPollAsync(occurrence, canonicalChat, ct);
        }

        // Публикуем зеркала во все остальные чаты
        var mirrorChats = activeChats
            .Where(c => c.ChatId != canonicalChat?.ChatId)
            .Where(c => ShouldMirrorToChat(occurrence, c))
            .ToList();

        foreach (var chat in mirrorChats)
        {
            try
            {
                await PublishMirrorAsync(occurrence, chat, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OccurrencePublisher] Failed to publish mirror to chat {ChatId}", chat.ChatId);
                // Не прерываем публикацию в остальные чаты
            }
        }

        // Обновляем статус
        occurrence.Status = OccurrenceStatus.Published;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[OccurrencePublisher] Occurrence #{Id} published. Canonical: {CanonicalChat}, Mirrors: {MirrorCount}",
            occurrence.Id, canonicalChat?.ChatId, mirrorChats.Count);
    }

    private static TelegramChat? FindCanonicalChat(Occurrence occurrence, List<TelegramChat> chats)
    {
        return occurrence.Type switch
        {
            // Для занятий — ищем чат группы
            OccurrenceType.Lesson => chats.FirstOrDefault(c =>
                c.Purpose == TelegramChatPurpose.GroupPrimary
                && (occurrence.DanceGroupId == null || c.DanceGroupId == occurrence.DanceGroupId)),

            // Для вечеринок/поездок — ищем events_chat или flood_chat
            OccurrenceType.Party or OccurrenceType.Trip =>
                chats.FirstOrDefault(c => c.Purpose == TelegramChatPurpose.EventsChat)
                ?? chats.FirstOrDefault(c => c.Purpose == TelegramChatPurpose.FloodChat),

            // Для практик — group_primary или events_chat
            OccurrenceType.Practice =>
                chats.FirstOrDefault(c => c.Purpose == TelegramChatPurpose.GroupPrimary)
                ?? chats.FirstOrDefault(c => c.Purpose == TelegramChatPurpose.EventsChat),

            _ => chats.FirstOrDefault(c => c.Purpose == TelegramChatPurpose.GroupPrimary)
        };
    }

    private static bool ShouldMirrorToChat(Occurrence occurrence, TelegramChat chat)
    {
        // Admin space — никогда не зеркалируем
        if (chat.Purpose == TelegramChatPurpose.AdminSpace) return false;

        return occurrence.Type switch
        {
            // Занятия зеркалируем в feed и flood
            OccurrenceType.Lesson =>
                chat.Purpose is TelegramChatPurpose.AllLessonsFeed or TelegramChatPurpose.FloodChat,

            // Вечеринки зеркалируем во все чаты кроме group_primary одной конкретной группы
            OccurrenceType.Party or OccurrenceType.Trip => true,

            OccurrenceType.Practice =>
                chat.Purpose is TelegramChatPurpose.AllLessonsFeed or TelegramChatPurpose.FloodChat,

            _ => true
        };
    }

    private async Task PublishCanonicalPollAsync(Occurrence occurrence, TelegramChat chat, CancellationToken ct)
    {
        var question = BuildPollQuestion(occurrence);

        // Неанонимный poll: только из него получаем poll_answer updates
        var pollMsg = await _bot.SendPollAsync(
            chatId: chat.ChatId,
            question: question,
            options: ["✅ Иду", "❌ Не иду"],
            isAnonymous: false,
            type: Telegram.Bot.Types.Enums.PollType.Regular,
            allowsMultipleAnswers: false,
            cancellationToken: ct);

        var pollId = pollMsg.Poll?.Id ?? "";

        _db.OccurrencePublications.Add(new OccurrencePublication
        {
            OccurrenceId = occurrence.Id,
            TelegramChatId = chat.ChatId,
            TelegramMessageId = pollMsg.MessageId,
            TelegramPollId = pollId,
            PublicationType = PublicationTypes.CanonicalPoll,
            IsVotingSource = true,
            PublishedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[OccurrencePublisher] Canonical poll sent to chat {ChatId}, pollId={PollId}",
            chat.ChatId, pollId);
    }

    private async Task PublishMirrorAsync(Occurrence occurrence, TelegramChat chat, CancellationToken ct)
    {
        var text = BuildMirrorText(occurrence);
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Хочу прийти", $"join:{occurrence.Id}"),
                InlineKeyboardButton.WithCallbackData("ℹ️ Подробнее", $"info:{occurrence.Id}")
            }
        });

        var mirrorMsg = await _bot.SendTextMessageAsync(
            chatId: chat.ChatId,
            text: text,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);

        _db.OccurrencePublications.Add(new OccurrencePublication
        {
            OccurrenceId = occurrence.Id,
            TelegramChatId = chat.ChatId,
            TelegramMessageId = mirrorMsg.MessageId,
            TelegramPollId = null,
            PublicationType = PublicationTypes.Mirror,
            IsVotingSource = false,
            PublishedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[OccurrencePublisher] Mirror sent to chat {ChatId}", chat.ChatId);
    }

    private static string BuildPollQuestion(Occurrence o)
    {
        var timeStr = o.StartsAt.ToString("dd.MM HH:mm");
        return o.Type switch
        {
            OccurrenceType.Lesson =>
                $"📚 Занятие {o.Level ?? ""} — {timeStr}"
                + (o.DanceGroup != null ? $" ({o.DanceGroup.Name})" : ""),

            OccurrenceType.Party =>
                $"🎉 {o.Title ?? "Вечеринка"} — {timeStr}",

            OccurrenceType.Trip =>
                $"✈️ {o.Title ?? "Поездка"} — {timeStr}",

            OccurrenceType.Practice =>
                $"🕺 Практика — {timeStr}",

            _ => $"{o.Title ?? o.Type} — {timeStr}"
        };
    }

    private static string BuildMirrorText(Occurrence o)
    {
        var timeStr = o.StartsAt.ToString("dd.MM.yyyy HH:mm");
        var typeLabel = o.Type switch
        {
            OccurrenceType.Lesson => "📚 Занятие",
            OccurrenceType.Party => "🎉 Вечеринка",
            OccurrenceType.Trip => "✈️ Поездка",
            OccurrenceType.Practice => "🕺 Практика",
            _ => "📌 Ивент"
        };

        var lines = new List<string>
        {
            $"<b>{typeLabel}</b>",
            $"📅 {timeStr}"
        };

        if (o.DanceGroup != null) lines.Add($"👥 {o.DanceGroup.Name}");
        if (!string.IsNullOrEmpty(o.Level)) lines.Add($"📊 {o.Level}");
        if (o.Capacity.HasValue) lines.Add($"🎯 Мест: {o.Capacity}");
        if (!string.IsNullOrEmpty(o.Title)) lines.Add($"📌 {o.Title}");
        if (!string.IsNullOrEmpty(o.Notes)) lines.Add($"\n{o.Notes}");
        lines.Add("\n👇 Хочешь прийти?");

        return string.Join("\n", lines);
    }
}
