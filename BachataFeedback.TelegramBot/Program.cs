using BachataFeedback.Api.Data;
using BachataFeedback.TelegramBot.Handlers;
using BachataFeedback.TelegramBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        cfg.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        var cfg = ctx.Configuration;

        // Database
        var cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not found");
        services.AddDbContext<ApplicationDbContext>(opt =>
            opt.UseMySql(cs, ServerVersion.AutoDetect(cs)));

        // Telegram Bot Client
        var botToken = cfg["Telegram:BotToken"]
            ?? throw new InvalidOperationException("Telegram:BotToken not found");
        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));

        // Bot config
        services.AddSingleton<BotConfiguration>(sp =>
        {
            var adminIds = cfg.GetSection("Telegram:AdminUserIds").Get<long[]>() ?? [];
            return new BotConfiguration(botToken, adminIds);
        });

        // Services
        services.AddScoped<AttendanceTracker>();
        services.AddScoped<OccurrencePublisher>();

        // Handlers
        services.AddScoped<UpdateDispatcher>();
        services.AddScoped<CommandHandler>();
        services.AddScoped<PollAnswerHandler>();
        services.AddScoped<CallbackQueryHandler>();
        services.AddScoped<InlineQueryHandler>();

        // Polling hosted service
        services.AddHostedService<BotPollingService>();
    })
    .RunConsoleAsync();
