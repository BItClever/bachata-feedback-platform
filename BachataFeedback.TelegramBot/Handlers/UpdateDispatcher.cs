using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BachataFeedback.TelegramBot.Handlers;

/// <summary>
/// Маршрутизирует Telegram Update на нужный обработчик по типу обновления.
/// Также сохраняет все входящие текстовые сообщения в ChatMessages для аналитики.
/// </summary>
public class UpdateDispatcher
{
    private readonly CommandHandler _commandHandler;
    private readonly AnalyticsCommandHandler _analyticsCommandHandler;
    private readonly PollAnswerHandler _pollAnswerHandler;
    private readonly CallbackQueryHandler _callbackQueryHandler;
    private readonly InlineQueryHandler _inlineQueryHandler;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UpdateDispatcher> _logger;

    public UpdateDispatcher(
        CommandHandler commandHandler,
        AnalyticsCommandHandler analyticsCommandHandler,
        PollAnswerHandler pollAnswerHandler,
        CallbackQueryHandler callbackQueryHandler,
        InlineQueryHandler inlineQueryHandler,
        ApplicationDbContext db,
        ILogger<UpdateDispatcher> logger)
    {
        _commandHandler = commandHandler;
        _analyticsCommandHandler = analyticsCommandHandler;
        _pollAnswerHandler = pollAnswerHandler;
        _callbackQueryHandler = callbackQueryHandler;
        _inlineQueryHandler = inlineQueryHandler;
        _db = db;
        _logger = logger;
    }

    public async Task DispatchAsync(Update update, CancellationToken ct)
    {
        _logger.LogDebug("[Dispatch] UpdateType={Type} Id={Id}", update.Type, update.Id);

        switch (update.Type)
        {
            case UpdateType.Message when update.Message is { } msg:
                await HandleMessageAsync(msg, ct);
                break;

            case UpdateType.PollAnswer when update.PollAnswer is { } answer:
                await _pollAnswerHandler.HandleAsync(answer, ct);
                break;

            case UpdateType.CallbackQuery when update.CallbackQuery is { } cbq:
                await _callbackQueryHandler.HandleAsync(cbq, ct);
                break;

            case UpdateType.InlineQuery when update.InlineQuery is { } iq:
                await _inlineQueryHandler.HandleAsync(iq, ct);
                break;

            case UpdateType.MyChatMember when update.MyChatMember is { } mcm:
                HandleMyChatMember(mcm);
                break;

            default:
                _logger.LogDebug("[Dispatch] Unhandled update type: {Type}", update.Type);
                break;
        }
    }

    private async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        // Сохраняем сообщение в историю чата (для аналитики)
        await SaveMessageAsync(msg, ct);

        if (msg.Text?.StartsWith('/') == true)
        {
            // Команды аналитики: /summary, /profile, /analytics
            var cmd = msg.Text.Split(' ')[0].Split('@')[0].ToLowerInvariant();
            if (cmd is "/summary" or "/profile" or "/analytics")
                await _analyticsCommandHandler.HandleAsync(msg, ct);
            else if (cmd is "/help" or "/start" or "/next_event" or "/today" or "/tomorrow" or "/support"
                or "/add_lesson" or "/add_party" or "/publish" or "/close_poll" or "/occurrences" or "/cancel")
                await _commandHandler.HandleAsync(msg, ct);
            // Все остальные команды игнорируем
        }
    }

    private async Task SaveMessageAsync(Message msg, CancellationToken ct)
    {
        // Сохраняем только текстовые сообщения (или сообщения с подписью к медиа)
        var text = msg.Text ?? msg.Caption;
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            // Проверяем дубликат
            var exists = await _db.ChatMessages.AnyAsync(
                cm => cm.TelegramChatId == msg.Chat.Id && cm.TelegramMessageId == msg.MessageId, ct);
            if (exists) return;

            var chatMsg = new ChatMessage
            {
                TelegramChatId = msg.Chat.Id,
                TelegramMessageId = msg.MessageId,
                TelegramUserId = msg.From?.Id,
                Username = msg.From?.Username,
                FirstName = msg.From?.FirstName,
                LastName = msg.From?.LastName,
                Text = text,
                SentAt = msg.Date.ToUniversalTime(),
                IsForwarded = msg.ForwardOrigin != null,
                IsBot = msg.From?.IsBot ?? false
            };

            _db.ChatMessages.Add(chatMsg);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Dispatch] Failed to save message {ChatId}/{MsgId}", msg.Chat.Id, msg.MessageId);
        }
    }

    private void HandleMyChatMember(ChatMemberUpdated update)
    {
        _logger.LogInformation(
            "[MyChatMember] Chat {ChatId} ({ChatTitle}): status changed {OldStatus} → {NewStatus}",
            update.Chat.Id,
            update.Chat.Title,
            update.OldChatMember.Status,
            update.NewChatMember.Status);
    }
}
