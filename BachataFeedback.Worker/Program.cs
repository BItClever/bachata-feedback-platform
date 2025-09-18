using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        cfg.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        // Берём строку подключения строго и явно (ENV имеет приоритет)
        var cs = ResolveConnectionString(ctx.Configuration);
        Console.WriteLine($"[worker] Using DB: {MaskConnectionString(cs)}");
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseMySql(cs, ServerVersion.AutoDetect(cs)));

        // Единственный провайдер LLM: LM Studio
        services.AddSingleton<ILLMClient, LMStudioClient>();

        services.AddHostedService<ModerationWorker>();
    })
    .RunConsoleAsync();

static string ResolveConnectionString(IConfiguration cfg)
{
    // 1) ENV прямой
    var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    if (!string.IsNullOrWhiteSpace(fromEnv))
        return fromEnv.Trim();

    // 2) Конфиг
    var fromCfg = cfg.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(fromCfg))
        return fromCfg.Trim();

    throw new InvalidOperationException("DefaultConnection is not provided. Set ENV ConnectionStrings__DefaultConnection or add to appsettings.");
}

static string MaskConnectionString(string cs)
{
    try
    {
        var parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var kv = parts[i].Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals("Password", StringComparison.OrdinalIgnoreCase))
                parts[i] = $"{kv[0]}=****";
        }
        return string.Join(';', parts);
    }
    catch { return "***"; }
}

public record ModerationMessage(string TargetType, int TargetId);

public class ModerationWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;

    public ModerationWorker(IServiceProvider sp, IConfiguration cfg)
    {
        _sp = sp;
        _cfg = cfg;
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
            ClientProvidedName = "moderation-worker"
        };
        var exchange = s.GetValue<string>("Exchange") ?? "moderation";
        var queue = s.GetValue<string>("Queue") ?? "moderation.jobs";
        var pollDelayMs = s.GetValue<int?>("PollDelayMs") ?? 1000;

        Console.WriteLine($"[worker] RabbitMQ: {factory.HostName}:{factory.Port} exchange={exchange} queue={queue}");

        IConnection? conn = null;
        IChannel? ch = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Поддерживаем подключение живым: если закрыто — создаём заново
                if (conn == null || conn.IsOpen == false)
                {
                    conn?.Dispose();
                    ch?.Dispose();

                    conn = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
                    ch = await conn.CreateChannelAsync(cancellationToken: stoppingToken);

                    // Durable + lazy queue
                    await ch.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false, arguments: null, noWait: false, cancellationToken: stoppingToken);
                    var args = new Dictionary<string, object?> { ["x-queue-mode"] = "lazy" };
                    await ch.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: args, passive: false, noWait: false, cancellationToken: stoppingToken);
                    await ch.QueueBindAsync(queue, exchange, routingKey: "", arguments: null, cancellationToken: stoppingToken);

                    Console.WriteLine("[worker] Connected to RabbitMQ and topology declared.");
                }

                // Poll 1 msg
                var get = await ch!.BasicGetAsync(queue, autoAck: false, cancellationToken: stoppingToken);
                if (get == null)
                {
                    await Task.Delay(pollDelayMs, stoppingToken);
                    continue;
                }

                ModerationMessage? msg = null;
                try { msg = JsonSerializer.Deserialize<ModerationMessage>(get.Body.ToArray()); }
                catch (Exception ex) { Console.WriteLine("[worker] JSON deserialize error: " + ex.Message); }

                if (msg == null)
                {
                    await ch.BasicNackAsync(get.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                Console.WriteLine($"[worker] Processing {msg.TargetType} #{msg.TargetId}");

                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var llm = scope.ServiceProvider.GetRequiredService<ILLMClient>();

                try
                {
                    if (msg.TargetType == "Review")
                        await ModerateReviewAsync(db, llm, msg.TargetId, stoppingToken);
                    else if (msg.TargetType == "EventReview")
                        await ModerateEventReviewAsync(db, llm, msg.TargetId, stoppingToken);
                    else
                        Console.WriteLine("[worker] Unknown TargetType: " + msg.TargetType);

                    var job = await db.ModerationJobs
                        .OrderByDescending(j => j.CreatedAt)
                        .FirstOrDefaultAsync(j => j.TargetType == msg.TargetType && j.TargetId == msg.TargetId, stoppingToken);
                    if (job != null)
                    {
                        job.Status = "Done";
                        job.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);
                    }

                    await ch.BasicAckAsync(get.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    Console.WriteLine($"[worker] Done {msg.TargetType} #{msg.TargetId}");
                }
                catch (HttpRequestException httpEx)
                {
                    Console.WriteLine("[worker] LLM HTTP error: " + httpEx.Message);
                    await ch.BasicNackAsync(get.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    await Task.Delay(pollDelayMs, stoppingToken);
                }
                catch (TaskCanceledException tce)
                {
                    Console.WriteLine("[worker] LLM timeout: " + tce.Message);
                    await ch.BasicNackAsync(get.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    await Task.Delay(pollDelayMs, stoppingToken);
                }
                catch (DbUpdateException dbe)
                {
                    Console.WriteLine("[worker] DB error: " + dbe.Message);
                    await ch.BasicNackAsync(get.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    await Task.Delay(500, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[worker] Processing error: " + ex.Message);

                    try
                    {
                        var job = await db.ModerationJobs
                            .OrderByDescending(j => j.CreatedAt)
                            .FirstOrDefaultAsync(j => j.TargetType == msg.TargetType && j.TargetId == msg.TargetId, stoppingToken);
                        if (job != null)
                        {
                            job.Status = "Error";
                            job.Attempts += 1;
                            job.LastError = ex.Message;
                            job.UpdatedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);
                        }
                    }
                    catch { /* ignore nested */ }

                    await ch.BasicNackAsync(get.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    await Task.Delay(500, stoppingToken);
                }
            }
            catch (AlreadyClosedException ace)
            {
                Console.WriteLine("[worker] RMQ closed: " + ace.Message + ". Reconnecting...");
                await Task.Delay(1000, stoppingToken);
            }
            catch (BrokerUnreachableException bue)
            {
                Console.WriteLine("[worker] RMQ unreachable: " + bue.Message + ". Retry...");
                await Task.Delay(1000, stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[worker] Loop error: " + ex.Message);
                await Task.Delay(1000, stoppingToken);
            }
        }

        // финальная очистка
        try { ch?.Dispose(); } catch { }
        try { conn?.Dispose(); } catch { }
    }

    private static async Task ModerateReviewAsync(ApplicationDbContext db, ILLMClient llm, int id, CancellationToken ct)
    {
        var r = await db.Reviews
            .AsTracking()
            .Include(x => x.Reviewer)
            .Include(x => x.Reviewee)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return;

        var text = BuildText(r.TextReview, r.LeadRatings, r.FollowRatings);
        var verdict = await llm.ClassifyAsync(text, ct);

        ApplyVerdict(r, verdict);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ModerateEventReviewAsync(ApplicationDbContext db, ILLMClient llm, int id, CancellationToken ct)
    {
        var r = await db.EventReviews.AsTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return;

        var text = (r.TextReview ?? string.Empty) + (string.IsNullOrEmpty(r.Ratings) ? "" : "\n" + r.Ratings);
        var verdict = await llm.ClassifyAsync(text, ct);

        ApplyVerdict(r, verdict);
        await db.SaveChangesAsync(ct);
    }

    private static string BuildText(string? text, string? leadRatingsJson, string? followRatingsJson)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
        if (!string.IsNullOrWhiteSpace(leadRatingsJson)) sb.AppendLine("LeadRatings: " + leadRatingsJson);
        if (!string.IsNullOrWhiteSpace(followRatingsJson)) sb.AppendLine("FollowRatings: " + followRatingsJson);
        return sb.ToString().Trim();
    }

    private static void ApplyVerdict(Review r, ModerationVerdict v)
    {
        r.ModerationLevel = v.Level switch
        {
            "Green" => ModerationLevel.Green,
            "Yellow" => ModerationLevel.Yellow,
            "Red" => ModerationLevel.Red,
            _ => ModerationLevel.Yellow
        };
        r.ModerationSource = ModerationSource.LLM;
        r.ModeratedAt = DateTime.UtcNow;
        r.ModerationReason = v.Reason;
    }

    private static void ApplyVerdict(BachataFeedback.Core.Models.EventReview r, ModerationVerdict v)
    {
        r.ModerationLevel = v.Level switch
        {
            "Green" => ModerationLevel.Green,
            "Yellow" => ModerationLevel.Yellow,
            "Red" => ModerationLevel.Red,
            _ => ModerationLevel.Yellow
        };
        r.ModerationSource = ModerationSource.LLM;
        r.ModeratedAt = DateTime.UtcNow;
        r.ModerationReason = v.Reason;
    }
}

public record ModerationVerdict(string Level, string Reason, string[] Categories);

public interface ILLMClient
{
    Task<ModerationVerdict> ClassifyAsync(string input, CancellationToken ct);
}

// LM Studio (OpenAI совместимый /v1/chat/completions)
public class LMStudioClient : ILLMClient
{
    private readonly HttpClient _http = new();
    private readonly string _base;
    private readonly string _model;
    private readonly int _max;
    private readonly double _temp;

    public LMStudioClient(IConfiguration cfg)
    {
        var s = cfg.GetSection("LLM");
        _base = s.GetValue<string>("BaseUrl") ?? "http://localhost:1234";
        _model = s.GetValue<string>("Model") ?? "local-model";
        _max = s.GetValue<int?>("MaxTokens") ?? 512;
        _temp = s.GetValue<double?>("Temperature") ?? 0.0;
    }

    public async Task<ModerationVerdict> ClassifyAsync(string input, CancellationToken ct)
    {
        var system = @"You are a strict but fair moderator for social dance (bachata) feedback.
Return ONLY valid JSON matching this schema:
{""level"":""Green|Yellow|Red"", ""reason"":""string (<=200 chars)"", ""categories"":[""optional-tags""]}.
Rules:
- Green: polite/constructive, even if negative.
- Yellow: borderline rude, sarcasm, mild profanity, hygiene remarks stated factually; still show to users.
- Red: direct insults, slurs, harassment, threats, sexual harassment.
Context: Feedback may mention technique, musicality, timing, connection, hygiene (e.g., smell). Allow factual hygiene notes unless insulting.";
        var user = $"Analyze this feedback and respond with JSON only:\n\"\"\"{input}\"\"\"";

        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = _temp,
            max_tokens = _max,
            stream = false
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var raw = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
        return ParseVerdict(content);
    }

    private static ModerationVerdict ParseVerdict(string llmText)
    {
        var json = ExtractJson(llmText);
        var el = JsonDocument.Parse(json).RootElement;
        var level = el.GetProperty("level").GetString() ?? "Yellow";
        var reason = el.GetProperty("reason").GetString() ?? "";
        var cats = el.TryGetProperty("categories", out var c) && c.ValueKind == JsonValueKind.Array
            ? c.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray()
            : Array.Empty<string>();
        return new ModerationVerdict(level, reason, cats);
    }

    private static string ExtractJson(string s)
    {
        var start = s.IndexOf('{');
        var end = s.LastIndexOf('}');
        if (start >= 0 && end >= start) return s.Substring(start, end - start + 1);
        return @"{""level"":""Yellow"",""reason"":""fallback"",""categories"":[]}";
    }
}