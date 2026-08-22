using Microsoft.Extensions.Options;
using NonStop.SitUpStraight.Bot.BackgroundServices;
using NonStop.SitUpStraight.Bot.Configurations;
using NonStop.SitUpStraight.Bot.Db;
using NonStop.SitUpStraight.Bot.Services;
using Serilog;
using Telegram.Bot;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();

// Configuration
builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection("BotConfiguration"));

// Typed TelegramBotClient with IHttpClientFactory
builder.Services.AddHttpClient("telegram_bot_client").RemoveAllLoggers()
    .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
    {
        var botConfig = sp.GetService<IOptions<BotConfiguration>>()?.Value;
        if (string.IsNullOrEmpty(botConfig?.BotToken))
        {
            throw new InvalidOperationException("Bot token is not configured in BotConfiguration:BotToken");
        }
        var options = new TelegramBotClientOptions(botConfig.BotToken);
        return new TelegramBotClient(options, httpClient);
    });

// Database & Domain Services
builder.Services.AddDbContext<SitUpStraightDbContext>();
builder.Services.AddSingleton<ITimezonesService, TimezonesService>();
builder.Services.AddSingleton<IMarkupService, MarkupService>();

// Update Handler & Background Services
builder.Services.AddScoped<UpdateHandler>();
builder.Services.AddHostedService<BotPollingService>();
builder.Services.AddHostedService<PostureReminderWorker>();

var app = builder.Build();

app.Run();