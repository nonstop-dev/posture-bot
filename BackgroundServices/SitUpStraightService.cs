using Microsoft.EntityFrameworkCore;
using NonStop.SitUpStraight.Bot.Constants;
using NonStop.SitUpStraight.Bot.Db;
using NonStop.SitUpStraight.Bot.Models;
using NonStop.SitUpStraight.Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NonStop.SitUpStraight.Bot.BackgroundServices;

public class SitUpStraightService(
    ILogger<SitUpStraightService> logger,
    IServiceScopeFactory serviceScopeFactory,
    ITimezonesService timezonesService
    ) : BackgroundService, IDisposable
{
    private const int TotalMinutesCount = 60;
    private List<Subscriber> _subscribers = [];
    private readonly List<Timezone> _timezones = timezonesService.GetTimezones();
    private TelegramBotClient? _botClient;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseCreatedAndMigrated(stoppingToken);
        InitializeBotClient();
        await RestoreSubscribersAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Worker: {Count}", _subscribers.Count);
            var minutes = TotalMinutesCount - DateTime.UtcNow.Minute;
            var delay = (minutes * 60) - DateTime.UtcNow.Second;
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

            if (_subscribers.Count == 0)
                continue;

            var currentHourUtc = DateTime.Now.Hour;

            List<(Subscriber Subscriber, string Message)> subscribersWithMessages = [];
            
            foreach (var s in _subscribers)
            {
                // todo: improve it might be negative
                logger.LogInformation("Subscriber: {@Subscriber}", s);
                var startHourUtc = s.StartHour - s.Offset;
                var endHourUtc = s.EndHour - s.Offset;
                if (currentHourUtc > startHourUtc && currentHourUtc < endHourUtc)
                {
                    subscribersWithMessages.Add((s, Messages.Message));
                }
                else if (startHourUtc == currentHourUtc)
                {
                    subscribersWithMessages.Add((s, Messages.MorningMessage));
                }
                else if (endHourUtc == currentHourUtc)
                {
                    subscribersWithMessages.Add((s, Messages.EveningMessage));
                }
            }

            var tasks = subscribersWithMessages.Select(async kv =>
                await SendMessageAsync(kv.Subscriber.ChatId, kv.Message, null, _cancellationTokenSource.Token));

            await Task.WhenAll(tasks);

            logger.LogInformation("Worker: All messages sent");
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
        try
        {
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
                            var buttons = new List<InlineKeyboardButton[]>();
                            foreach (var t in _timezones)
                            {
                                var data = $"{t.Offset}--{t.Title}";
                                var button = new InlineKeyboardButton[]
                                {
                                    InlineKeyboardButton.WithCallbackData(t.Title, data)
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
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Свершается магия", cancellationToken: cancellationToken);
                    var chat = callbackQuery.Message.Chat;
                    var callbackData = callbackQuery.Data.Split("--");
                    var offset = int.Parse(callbackData[0]);
                    var title = callbackData[1];
                    await UpdateSubscriberTimezone(chat.Id, offset, cancellationToken);

                    await SendMessageAsync(
                        chat.Id,
                        $"Выбрана таймзона: {title}",
                        null,
                        cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while handling update");
        }
    }

    private async Task HandleStartCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
            return;

        await AddSubscriberAsync(chatId, cancellationToken);

        await SendMessageAsync(chatId, Messages.Message, null, cancellationToken);
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

    private async Task UpdateSubscriberTimezone(long chatId, int offset, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId, cancellationToken], cancellationToken: cancellationToken);
        if (subscriberFromDb == null)
            return;

        subscriberFromDb.Offset = offset;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Subscriber's timezone has been updated");

        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
        {
            subscriber.Offset = offset;
        }
    }
    public override void Dispose()
    {
        _cancellationTokenSource.Cancel();
        // todo
        // GC.SuppressFinalize(this);
    }
}