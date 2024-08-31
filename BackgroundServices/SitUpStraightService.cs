using Microsoft.EntityFrameworkCore;
using NonStop.SitUpStraight.Bot.Constants;
using NonStop.SitUpStraight.Bot.Db;
using NonStop.SitUpStraight.Bot.Extensions;
using NonStop.SitUpStraight.Bot.Helpers;
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
    ITimezonesService timezonesService,
    IMarkupService markupService
    ) : BackgroundService, IDisposable
{
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
            // todo: delete this log
            logger.LogInformation("Worker: {Count}", _subscribers.Count);
            var minutes = TotalMinutesCount - DateTime.UtcNow.Minute;
            var delay = (minutes * 60) - DateTime.UtcNow.Second;
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

            if (_subscribers.Count == 0)
                continue;

            var now = DateTime.UtcNow;
            var currentHourUtc = now.Hour;
            List<(Subscriber Subscriber, string Message)> subscribersWithMessages = [];

            foreach (var s in _subscribers)
            {
                var dayNumber = now.DayOfWeek.ToDayNumber();
                if (dayNumber > s.DaysPerWeek)
                    continue;

                var startHourUtc = s.StartHourUtc;
                var endHourUtc = s.EndHourUtc;
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
                            var timezonesMarkup = markupService.GetTimezonesMarkup();
                            await SendMessageAsync(
                                message.Chat.Id,
                                BotCommands.SelectTimezone,
                                timezonesMarkup,
                                cancellationToken);
                            break;
                        case BotCommands.SelectDays:
                            var daysMarkup = markupService.GetDaysMarkup();
                            await SendMessageAsync(
                                message.Chat.Id,
                                BotCommands.SelectDays,
                                daysMarkup,
                                cancellationToken);
                            break;
                        case BotCommands.SelectHours:
                            var hoursMarkup = markupService.GetHoursMarkup();
                            await SendMessageAsync(
                                message.Chat.Id,
                                BotCommands.SelectHours,
                                hoursMarkup,
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
                    var command = callbackData[0];
                    switch (command)
                    {
                        case MarkupCommands.Timezone:
                            var id = int.Parse(callbackData[1]);
                            var timezone = timezonesService.GetTimezone(id);
                            await UpdateSubscriberTimezone(chat.Id, timezone.Offset, cancellationToken);

                            await SendMessageAsync(
                                chat.Id,
                                $"Выбрана таймзона: {timezone.Title}",
                                null,
                                cancellationToken);
                            break;
                        case MarkupCommands.Days:
                            var daysPerWeek = int.Parse(callbackData[1]);
                            await UpdateSubscriberDaysAsync(chat.Id, daysPerWeek, cancellationToken);
                            await SendMessageAsync(
                                chat.Id,
                                $"Ровная спина будет {daysPerWeek} дней в неделю",
                                null,
                                cancellationToken);
                            break;
                        case MarkupCommands.Hours:
                            if (callbackData[1] == "custom")
                            {
                                await SendMessageAsync(
                                    chat.Id,
                                    $"Скоро будет. А пока: выпрями спину!",
                                    null,
                                    cancellationToken);
                                // todo: customize
                            }
                            else
                            {
                                var userStartHour = int.Parse(callbackData[1]);
                                var userEndHour = int.Parse(callbackData[2]);
                                var subscriber = _subscribers.First(x => x.ChatId == chat.Id);
                                var startHourUtc = TimeHelper.GetStartHourUtc(userStartHour, subscriber.Offset);
                                var endHourUtc = TimeHelper.GetEndHourUtc(userEndHour, subscriber.Offset);
                                await UpdateSubscriberHours(chat.Id, startHourUtc, endHourUtc, cancellationToken);

                                await SendMessageAsync(
                                    chat.Id,
                                    $"Выбрано время с {userStartHour} по {userEndHour}",
                                    null,
                                    cancellationToken);
                            }
                            break;
                    }
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
        replyMarkup ??= markupService.GetDefaultMarkup();
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

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId, cancellationToken], cancellationToken);
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

    private async Task UpdateSubscriberHours(long chatId, int startHourUtc, int endHourUtc, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId, cancellationToken], cancellationToken);
        if (subscriberFromDb == null)
            return;

        subscriberFromDb.StartHourUtc = startHourUtc;
        subscriberFromDb.EndHourUtc = endHourUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Subscriber's hours has been updated");

        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
        {
            subscriber.StartHourUtc = startHourUtc;
            subscriber.EndHourUtc = endHourUtc;
        }
    }

    private async Task UpdateSubscriberDaysAsync(long chatId, int daysPerWeek, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId, cancellationToken], cancellationToken);
        if (subscriberFromDb == null)
            return;

        subscriberFromDb.DaysPerWeek = daysPerWeek;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Subscriber's days per week has been updated");

        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
        {
            subscriber.DaysPerWeek = daysPerWeek;
        }
    }

    public override void Dispose()
    {
        _cancellationTokenSource.Cancel();
        // todo
        // GC.SuppressFinalize(this);
    }
}