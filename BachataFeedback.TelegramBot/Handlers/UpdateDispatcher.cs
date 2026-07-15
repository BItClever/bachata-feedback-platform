using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BachataFeedback.TelegramBot.Handlers;

/// <summary>
/// Маршрутизирует Telegram Update на нужный обработчик по типу обновления.
/// </summary>
public class UpdateDispatcher
{
    private readonly CommandHandler _commandHandler;
    private readonly PollAnswerHandler _pollAnswerHandler;
    private readonly CallbackQueryHandler _callbackQueryHandler;
    private readonly InlineQueryHandler _inlineQueryHandler;
    private readonly ILogger<UpdateDispatcher> _logger;

    public UpdateDispatcher(
        CommandHandler commandHandler,
        PollAnswerHandler pollAnswerHandler,
        CallbackQueryHandler callbackQueryHandler,
        InlineQueryHandler inlineQueryHandler,
        ILogger<UpdateDispatcher> logger)
    {
        _commandHandler = commandHandler;
        _pollAnswerHandler = pollAnswerHandler;
        _callbackQueryHandler = callbackQueryHandler;
        _inlineQueryHandler = inlineQueryHandler;
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
        // Обрабатываем только команды (текстовые сообщения начинающиеся с '/')
        if (msg.Text?.StartsWith('/') == true)
        {
            await _commandHandler.HandleAsync(msg, ct);
        }
    }

    private void HandleMyChatMember(ChatMemberUpdated update)
    {
        // Логируем изменение статуса бота в чате.
        // Регистрация чатов происходит вручную через API, но можно расширить здесь.
        _logger.LogInformation(
            "[MyChatMember] Chat {ChatId} ({ChatTitle}): status changed {OldStatus} → {NewStatus}",
            update.Chat.Id,
            update.Chat.Title,
            update.OldChatMember.Status,
            update.NewChatMember.Status);
    }
}
