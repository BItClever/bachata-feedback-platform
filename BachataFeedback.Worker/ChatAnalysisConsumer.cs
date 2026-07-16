using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

/// <summary>
/// Читает задачи из очереди chat_analysis.jobs и вызывает LLM для анализа.
/// Поддерживает типы: Summary, UserProfile, ChatAnalytics.
/// </summary>
public class ChatAnalysisConsumer : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;
    private readonly ILogger<ChatAnalysisConsumer> _logger;

    public ChatAnalysisConsumer(IServiceProvider sp, IConfiguration cfg, ILogger<ChatAnalysisConsumer> logger)
    {
        _sp = sp;
        _cfg = cfg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var s = _cfg.GetSection("RabbitMQ");
        var factory = new ConnectionFactory
        {
            HostName = s.GetValue<string>("Host") ?? "localhost",
            Port = s.GetValue<int?>("Port") ?? 5672,
            UserName = s.GetValue<string>("User") ?? "guest",
            Password = s.GetValue<string>("Password") ?? "guest",
            ClientProvidedName = "chat-analysis-consumer"
        };
        const string exchange = "chat_analysis";
        const string queue = "chat_analysis.jobs";
        var pollDelayMs = s.GetValue<int?>("PollDelayMs") ?? 1000;

        IConnection? conn = null;
        IChannel? ch = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (conn == null || conn.IsOpen == false)
                {
                    conn?.Dispose(); ch?.Dispose();
                    conn = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
                    ch = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
                    await ch.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false, arguments: null, noWait: false, cancellationToken: stoppingToken);
                    var args = new Dictionary<string, object?> { ["x-queue-mode"] = "lazy" };
                    await ch.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: args, passive: false, noWait: false, cancellationToken: stoppingToken);
                    await ch.QueueBindAsync(queue, exchange, routingKey: "", arguments: null, cancellationToken: stoppingToken);
                    _logger.LogInformation("[ChatAnalysis] Connected to RabbitMQ");
                }

                var get = await ch!.BasicGetAsync(queue, autoAck: false, cancellationToken: stoppingToken);
                if (get == null) { await Task.Delay(pollDelayMs, stoppingToken); continue; }

                AnalysisJobMessage? msg = null;
                try { msg = JsonSerializer.Deserialize<AnalysisJobMessage>(get.Body.ToArray()); }
                catch { _logger.LogWarning("[ChatAnalysis] JSON parse error"); }

                if (msg == null)
                {
                    await ch.BasicNackAsync(get.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                _logger.LogInformation("[ChatAnalysis] Processing job #{Id} type={Type}", msg.JobId, msg.Type);
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var llm = scope.ServiceProvider.GetRequiredService<ILLMClient>();

                var job = await db.AnalysisJobs.FindAsync(new object[] { msg.JobId }, stoppingToken);
                if (job == null)
                {
                    await ch.BasicAckAsync(get.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    continue;
                }

                try
                {
                    job.Status = AnalysisJobStatus.Processing;
                    await db.SaveChangesAsync(stoppingToken);

                    var result = await ProcessJobAsync(job, db, llm, stoppingToken);
                    job.ResultText = result;
                    job.Status = AnalysisJobStatus.Done;
                    job.CompletedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);

                    await ch.BasicAckAsync(get.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    _logger.LogInformation("[ChatAnalysis] Done job #{Id}", job.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ChatAnalysis] Failed job #{Id}", msg.JobId);
                    if (job != null) { job.Status = AnalysisJobStatus.Failed; job.ErrorMessage = ex.Message; }
                    try { await db.SaveChangesAsync(stoppingToken); } catch { }
                    await ch.BasicNackAsync(get.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                }
            }
            catch (AlreadyClosedException) { await Task.Delay(1000, stoppingToken); conn = null; }
            catch (BrokerUnreachableException) { await Task.Delay(2000, stoppingToken); conn = null; }
            catch (Exception ex) { _logger.LogError(ex, "[ChatAnalysis] Loop error"); await Task.Delay(1000, stoppingToken); }
        }

        try { ch?.Dispose(); } catch { }
        try { conn?.Dispose(); } catch { }
    }

    private async Task<string> ProcessJobAsync(AnalysisJob job, ApplicationDbContext db, ILLMClient llm, CancellationToken ct)
    {
        return job.Type switch
        {
            AnalysisJobType.Summary => await BuildSummaryAsync(job, db, llm, ct),
            AnalysisJobType.UserProfile => await BuildUserProfileAsync(job, db, llm, ct),
            AnalysisJobType.ChatAnalytics => await BuildChatAnalyticsAsync(job, db, llm, ct),
            _ => "Неизвестный тип анализа."
        };
    }

    private async Task<string> BuildSummaryAsync(AnalysisJob job, ApplicationDbContext db, ILLMClient llm, CancellationToken ct)
    {
        var messages = await db.ChatMessages
            .Where(cm => cm.TelegramChatId == job.ReplyToChatId && cm.Text != null && !cm.IsBot)
            .OrderByDescending(cm => cm.SentAt)
            .Take(job.MessageCount)
            .OrderBy(cm => cm.SentAt)
            .ToListAsync(ct);

        if (!messages.Any()) return "Нет сообщений для анализа.";

        var transcript = BuildTranscript(messages, maxChars: 12000);
        var prompt = "Кратко изложи суть следующих сообщений из Telegram-чата школы бачаты. "
            + "Выдели главные темы, важные вопросы и решения. Ответ на русском языке, не более 500 слов:\n\n"
            + transcript;

        return await llm.SummarizeAsync(prompt, ct);
    }

    private async Task<string> BuildUserProfileAsync(AnalysisJob job, ApplicationDbContext db, ILLMClient llm, CancellationToken ct)
    {
        if (!job.TargetTelegramUserId.HasValue) return "Не указан пользователь для профилирования.";

        var messages = await db.ChatMessages
            .Where(cm => cm.TelegramChatId == job.ReplyToChatId
                && cm.TelegramUserId == job.TargetTelegramUserId.Value
                && cm.Text != null)
            .OrderByDescending(cm => cm.SentAt)
            .Take(job.MessageCount)
            .OrderBy(cm => cm.SentAt)
            .ToListAsync(ct);

        if (!messages.Any()) return "Нет сообщений для профилирования.";

        var transcript = BuildTranscript(messages, maxChars: 8000, includeUserName: false);
        var prompt = "Проанализируй сообщения участника чата школы бачаты и составь его профиль. "
            + "Оцени: уровень активности, основные темы общения, характер вопросов, тональность, "
            + "признаки токсичности (0-10), интересы, стиль общения. "
            + "Формат ответа — JSON:\n"
            + "{ \"activity_level\": \"высокая|средняя|низкая\", "
            + "\"main_topics\": [...], \"tone\": \"...\", "
            + "\"toxicity_score\": 0-10, "
            + "\"frequent_questions\": [...], "
            + "\"interests\": [...], \"summary\": \"...\" }\n\n"
            + transcript;

        var jsonResult = await llm.SummarizeAsync(prompt, ct);

        var profile = await db.UserChatProfiles.FirstOrDefaultAsync(
            p => p.TelegramUserId == job.TargetTelegramUserId.Value && p.TelegramChatId == job.ReplyToChatId, ct);

        if (profile == null)
        {
            profile = new UserChatProfile
            {
                TelegramUserId = job.TargetTelegramUserId.Value,
                TelegramChatId = job.ReplyToChatId,
                DisplayName = job.TargetUserDisplayName,
                Username = messages.FirstOrDefault()?.Username
            };
            db.UserChatProfiles.Add(profile);
        }

        profile.AnalysisJson = jsonResult;
        profile.AnalyzedMessagesCount = messages.Count;
        profile.LastAnalyzedAt = DateTime.UtcNow;
        profile.Summary = ExtractSummaryFromJson(jsonResult);
        await db.SaveChangesAsync(ct);

        return FormatProfileResponse(job.TargetUserDisplayName ?? "Unknown", jsonResult);
    }

    private async Task<string> BuildChatAnalyticsAsync(AnalysisJob job, ApplicationDbContext db, ILLMClient llm, CancellationToken ct)
    {
        var messages = await db.ChatMessages
            .Where(cm => cm.TelegramChatId == job.ReplyToChatId && cm.Text != null && !cm.IsBot)
            .OrderByDescending(cm => cm.SentAt)
            .Take(job.MessageCount)
            .OrderBy(cm => cm.SentAt)
            .ToListAsync(ct);

        if (!messages.Any()) return "Нет сообщений для анализа.";

        var transcript = BuildTranscript(messages, maxChars: 12000);
        var prompt = "Проанализируй переписку в Telegram-чате школы бачаты. "
            + "Определи: главные темы, наиболее частые вопросы, общую тональность, признаки конфликтов или токсичности. "
            + "Выдели паттерны поведения. Ответ на русском языке, структурированно, не более 600 слов:\n\n"
            + transcript;

        return await llm.SummarizeAsync(prompt, ct);
    }

    private static string BuildTranscript(List<ChatMessage> messages, int maxChars, bool includeUserName = true)
    {
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            if (sb.Length >= maxChars) break;
            var name = includeUserName
                ? (m.Username != null ? $"@{m.Username}" : (m.FirstName ?? "User"))
                : "User";
            var line = $"[{m.SentAt:dd.MM HH:mm}] {name}: {m.Text?.Replace("\n", " ").Trim()}\n";
            if (sb.Length + line.Length > maxChars) break;
            sb.Append(line);
        }
        return sb.ToString();
    }

    private static string ExtractSummaryFromJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("summary", out var s)) return s.GetString() ?? "";
        }
        catch { }
        return "";
    }

    private static string FormatProfileResponse(string name, string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var el = doc.RootElement;
            var sb = new StringBuilder();
            sb.AppendLine($"<b>👤 Профиль: {name}</b>");
            if (el.TryGetProperty("activity_level", out var al)) sb.AppendLine($"📈 Активность: {al.GetString()}");
            if (el.TryGetProperty("tone", out var tone)) sb.AppendLine($"💬 Тон: {tone.GetString()}");
            if (el.TryGetProperty("toxicity_score", out var tox)) sb.AppendLine($"⚠️ Токсичность: {tox.GetRawText()}/10");
            if (el.TryGetProperty("main_topics", out var topics) && topics.ValueKind == JsonValueKind.Array)
                sb.AppendLine($"📌 Темы: {string.Join(", ", topics.EnumerateArray().Select(x => x.GetString()))}");
            if (el.TryGetProperty("interests", out var interests) && interests.ValueKind == JsonValueKind.Array)
                sb.AppendLine($"🎯 Интересы: {string.Join(", ", interests.EnumerateArray().Select(x => x.GetString()))}");
            if (el.TryGetProperty("frequent_questions", out var fq) && fq.ValueKind == JsonValueKind.Array)
                sb.AppendLine($"❓ Частые вопросы: {string.Join("; ", fq.EnumerateArray().Select(x => x.GetString()))}");
            if (el.TryGetProperty("summary", out var summary)) sb.AppendLine($"\n📝 {summary.GetString()}");
            return sb.ToString().TrimEnd();
        }
        catch { return json; }
    }
}