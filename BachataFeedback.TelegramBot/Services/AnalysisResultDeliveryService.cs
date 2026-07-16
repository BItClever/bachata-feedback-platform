using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace BachataFeedback.TelegramBot.Services;

/// <summary>
/// Фоновый сервис, который каждые 3 секунды проверяет готовые AnalysisJob
/// и отправляет результаты обратно в Telegram-чаты.
/// </summary>
public class AnalysisResultDeliveryService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<AnalysisResultDeliveryService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    public AnalysisResultDeliveryService(
        IServiceProvider sp,
        ITelegramBotClient bot,
        ILogger<AnalysisResultDeliveryService> logger)
    {
        _sp = sp;
        _bot = bot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AnalysisDelivery] Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeliverReadyJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AnalysisDelivery] Delivery loop error");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task DeliverReadyJobsAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var jobs = await db.AnalysisJobs
            .Where(j => j.Status == AnalysisJobStatus.Done && !j.SentToChat)
            .Take(10)
            .ToListAsync(ct);

        foreach (var job in jobs)
        {
            try
            {
                var text = BuildResponseText(job);
                await _bot.SendMessage(job.ReplyToChatId, text,
                    parseMode: ParseMode.Html, cancellationToken: ct);

                job.SentToChat = true;
                _logger.LogInformation("[AnalysisDelivery] Sent job #{Id} to chat {ChatId}", job.Id, job.ReplyToChatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AnalysisDelivery] Failed to send job #{Id}", job.Id);
                // Помечаем как отправленный, чтобы не застрять в цикле
                job.SentToChat = true;
            }
        }

        if (jobs.Any())
            await db.SaveChangesAsync(ct);
    }

    private static string BuildResponseText(AnalysisJob job)
    {
        var header = job.Type switch
        {
            AnalysisJobType.Summary => $"📋 <b>Краткое содержание</b> (последние {job.MessageCount} сообщений)",
            AnalysisJobType.UserProfile => $"👤 <b>Профиль: {job.TargetUserDisplayName ?? "Unknown"}</b> (по {job.MessageCount} сообщениям)",
            AnalysisJobType.ChatAnalytics => $"📊 <b>LLM-аналитика чата</b> (последние {job.MessageCount} сообщений)",
            _ => "📄 <b>Результат анализа</b>"
        };

        var body = string.IsNullOrWhiteSpace(job.ResultText)
            ? "⚠️ Результат недоступен."
            : job.ResultText;

        var elapsed = job.CompletedAt.HasValue
            ? $"\n\n<i>⏱ Время анализа: {(job.CompletedAt.Value - job.RequestedAt).TotalSeconds:F1}с</i>"
            : "";

        return $"{header}\n\n{body}{elapsed}";
    }
}
