using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace BachataFeedback.TelegramBot.Services;

/// <summary>
/// Создаёт AnalysisJob в БД и публикует задачу в RabbitMQ-очередь chat_analysis.
/// </summary>
public class AnalysisJobPublisher
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AnalysisJobPublisher> _logger;

    public AnalysisJobPublisher(ApplicationDbContext db, IConfiguration cfg, ILogger<AnalysisJobPublisher> logger)
    {
        _db = db;
        _cfg = cfg;
        _logger = logger;
    }

    /// <summary>
    /// Создаёт AnalysisJob и ставит задачу в очередь.
    /// </summary>
    public async Task<AnalysisJob> EnqueueAsync(
        AnalysisJobType type,
        long chatId,
        int messageCount,
        long requestedByUserId,
        long? targetUserId = null,
        string? targetUserDisplayName = null,
        CancellationToken ct = default)
    {
        var job = new AnalysisJob
        {
            Type = type,
            Status = AnalysisJobStatus.Pending,
            ReplyToChatId = chatId,
            MessageCount = messageCount,
            TargetTelegramUserId = targetUserId,
            TargetUserDisplayName = targetUserDisplayName,
            RequestedByUserId = requestedByUserId,
            RequestedAt = DateTime.UtcNow
        };

        _db.AnalysisJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        await PublishToRabbitAsync(job, ct);

        _logger.LogInformation("[AnalysisJobPublisher] Enqueued job #{Id} type={Type} chat={ChatId}", job.Id, type, chatId);
        return job;
    }

    private async Task PublishToRabbitAsync(AnalysisJob job, CancellationToken ct)
    {
        var s = _cfg.GetSection("RabbitMQ");
        var factory = new ConnectionFactory
        {
            HostName = s.GetValue<string>("Host") ?? "localhost",
            Port = s.GetValue<int?>("Port") ?? 5672,
            UserName = s.GetValue<string>("User") ?? "guest",
            Password = s.GetValue<string>("Password") ?? "guest",
            ClientProvidedName = "telegram-bot"
        };

        const string exchange = "chat_analysis";
        const string queue = "chat_analysis.jobs";

        try
        {
            using var conn = await factory.CreateConnectionAsync(cancellationToken: ct);
            using var ch = await conn.CreateChannelAsync(cancellationToken: ct);

            await ch.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false,
                arguments: null, noWait: false, cancellationToken: ct);
            var args = new Dictionary<string, object?> { ["x-queue-mode"] = "lazy" };
            await ch.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: args, passive: false, noWait: false, cancellationToken: ct);
            await ch.QueueBindAsync(queue, exchange, routingKey: "", arguments: null, cancellationToken: ct);

            var msg = new AnalysisJobMessage(job.Id, job.ReplyToChatId, job.Type.ToString(),
                job.MessageCount, job.TargetTelegramUserId, job.TargetUserDisplayName);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
            var props = new BasicProperties { Persistent = true };
            await ch.BasicPublishAsync(exchange, routingKey: "", mandatory: false, basicProperties: props, body: body, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AnalysisJobPublisher] Failed to publish job #{Id} to RabbitMQ", job.Id);
            // Не бросаем дальше — job уже в БД, Worker сможет подхватить позже
        }
    }
}

public record AnalysisJobMessage(int JobId, long ChatId, string Type, int MessageCount, long? TargetUserId, string? TargetUserDisplayName);
