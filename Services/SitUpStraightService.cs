using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NonStop.SitUpStraight.Bot.Db;
using NonStop.SitUpStraight.Bot.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NonStop.SitUpStraight.Bot.Services;

public class SitUpStraightService(
    ILogger<SitUpStraightService> logger,
    IServiceScopeFactory serviceScopeFactory
    ) : BackgroundService, IDisposable
{
    private const string Message = "Выпрями спину!";
    private const int TotalMinutesCount = 60;
    private List<Subscriber> _subscribers = [];
    private TelegramBotClient? _botClient;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseCreatedAndMigrated(stoppingToken);
        InitializeBotClient();
        await RestoreSubscribersAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var minutes = TotalMinutesCount - DateTime.UtcNow.Minute;
            var delay = (minutes * 60) - DateTime.UtcNow.Second;
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

            if (_subscribers.Count == 0)
                continue;

            var currentHourUtc = DateTime.Now.Hour;

            var subscribersToSend = _subscribers.Where(subscriber =>
                currentHourUtc >= subscriber.StartHourUtc && currentHourUtc <= subscriber.EndHourUtc);

            var tasks = subscribersToSend.Select(async subscriber =>
                await SendMessageAsync(subscriber.ChatId, Message, null, _cancellationTokenSource.Token));

            await Task.WhenAll(tasks);
        }
    }

    private void InitializeBotClient()
    {
        // todo: take from settings
        _botClient = new TelegramBotClient("242464316:AAFxxhWAurba-hw526Uo6TxjO7WS8B7PIio");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.MyChatMember, UpdateType.CallbackQuery],
            ThrowPendingUpdates = true // do not handle messages while bot was offline
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cancellationTokenSource.Token
        );

        logger.LogInformation("Bot initialized");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // todo: add try catch
        var message = update.Message;

        switch (update.Type)
        {
            case UpdateType.MyChatMember:
                if (message is null)
                {
                    var memberChatId = update.MyChatMember?.Chat.Id;
                    if (memberChatId != null)
                        await RemoveSubscriberAsync(memberChatId.Value, cancellationToken);
                    return;
                }
                break;
            case UpdateType.Message:
                if (message is null)
                    return;
                switch (message.Text)
                {
                    case BotCommands.Start:
                        await HandleStartCommandAsync(message.Chat.Id, cancellationToken);
                        break;
                    case BotCommands.SelectTimezone:
                        var timezones1 = LoadTimezones();
                        var buttons = new List<InlineKeyboardButton[]>();
                        foreach (var timezone in timezones1)
                        {
                            var button = new InlineKeyboardButton[]
                            {
                                InlineKeyboardButton.WithCallbackData(timezone.Title, timezone.Offset.ToString())
                            };
                            buttons.Add(button);
                        }
                        var markup = new InlineKeyboardMarkup(buttons);
                        await SendMessageAsync(
                            message.Chat.Id,
                            BotCommands.SelectTimezone,
                            markup,
                            cancellationToken);
                        break;
                    case BotCommands.SelectDays:
                        await SendMessageAsync(
                            message.Chat.Id,
                            "Скоро будет, а пока: выпрями спину!",
                            null,
                            cancellationToken);
                        break;
                    default:
                        return;
                }
                break;
            case UpdateType.CallbackQuery:
                var callbackQuery = update.CallbackQuery;
                var chat = callbackQuery.Message.Chat;
                var timezones = LoadTimezones();
                var offset = int.Parse(callbackQuery.Data!);

                // await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);

                await SendMessageAsync(
                    chat.Id,
                    "Скоро будет! А сейчас: выпрями спину!",
                    null,
                    cancellationToken);
                break;
        }
    }

    private async Task HandleStartCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
            return;

        await AddSubscriberAsync(chatId, cancellationToken);

        await SendMessageAsync(chatId, Message, null, cancellationToken);
    }

    private async Task SendMessageAsync(long chatId, string message, IReplyMarkup? replyMarkup, CancellationToken cancellationToken)
    {
        if (replyMarkup == null)
        {
            replyMarkup = new ReplyKeyboardMarkup(
            new List<KeyboardButton[]>()
            {
                new KeyboardButton[]
                {
                    new(BotCommands.SelectTimezone),
                    new(BotCommands.SelectDays)
                }
            })
            { ResizeKeyboard = true };
        }
        await _botClient!.SendTextMessageAsync(chatId, message, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken cancellationToken)
    {
        var message = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.Message
        };

        logger.LogError("{Message}", message);
    
        return Task.CompletedTask;
    }

    private async Task AddSubscriberAsync(long chatId, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

        Subscriber newSubscriber = new() { ChatId = chatId };
        _subscribers.Add(newSubscriber);
        dbContext.Subscribers.Add(newSubscriber);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Subscriber has been added");
    }

    private async Task RemoveSubscriberAsync(long chatId, CancellationToken cancellationToken)
    {
        var subscriberToRemove = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriberToRemove != null)
        {
            _subscribers.Remove(subscriberToRemove);
        }

        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId, cancellationToken], cancellationToken: cancellationToken);
        if (subscriberFromDb == null)
            return;

        dbContext.Subscribers.Remove(subscriberFromDb);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Subscriber has been removed");
    }

    private async Task RestoreSubscribersAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();
        _subscribers = await dbContext.Subscribers.ToListAsync(cancellationToken);
        logger.LogInformation("Subscribers have been restored");
    }

    private async Task EnsureDatabaseCreatedAndMigrated(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migration completed");
    }

    // todo: use lazy async
    private List<Timezone> LoadTimezones()
    {
        using var reader = new StreamReader("Data/timezones.json");
        var json = reader.ReadToEnd();
        // todo: serialization settings should be placed separately
        var timezones = JsonSerializer.Deserialize<List<Timezone>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return timezones!;
    }

    public override void Dispose()
    {
        _cancellationTokenSource.Cancel();
        // GC.SuppressFinalize(this);
    }
}