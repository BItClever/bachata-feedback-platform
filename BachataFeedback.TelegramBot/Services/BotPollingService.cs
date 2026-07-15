using BachataFeedback.TelegramBot.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BachataFeedback.TelegramBot.Services;

/// <summary>
/// Фоновый сервис long polling.
/// Получает обновления от Telegram и передаёт UpdateDispatcher'у.
/// </summary>
public class BotPollingService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly IServiceProvider _sp;
    private readonly ILogger<BotPollingService> _logger;

    public BotPollingService(ITelegramBotClient bot, IServiceProvider sp, ILogger<BotPollingService> logger)
    {
        _bot = bot;
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[TelegramBot] Starting long polling...");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates =
            [
                UpdateType.Message,
                UpdateType.CallbackQuery,
                UpdateType.PollAnswer,
                UpdateType.InlineQuery,
                UpdateType.MyChatMember
            ],
            // Не обрабатываем накопившиеся обновления при старте
            DropPendingUpdates = false
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _bot.ReceiveAsync(
                    updateHandler: HandleUpdateAsync,
                    errorHandler: HandleErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TelegramBot] Polling loop error. Restarting in 5s...");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("[TelegramBot] Polling stopped.");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<UpdateDispatcher>();
            await dispatcher.DispatchAsync(update, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TelegramBot] Error handling update #{UpdateId}", update.Id);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        _logger.LogWarning(ex, "[TelegramBot] Polling error: {Message}", ex.Message);
        return Task.CompletedTask;
    }
}
