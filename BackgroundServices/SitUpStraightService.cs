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
        await InitializeBotClientAsync(stoppingToken);
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

    private async Task InitializeBotClientAsync(CancellationToken cancellationToken)
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

        await _botClient.SetMyDescriptionAsync("Выровняю спину даже верблюду!", cancellationToken: cancellationToken);

        logger.LogInformation("Bot initialized");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
    {
        try
        {
            await (update switch
            {
                { Message: { } message } => OnMessageAsync(message, cancellationToken),
                { CallbackQuery: { } callbackQuery } => OnCallbackQueryAsync(callbackQuery, cancellationToken),
                { MyChatMember: { } myChatMember } => OnMyChatMemberAsync(myChatMember, cancellationToken),
                _ => Task.CompletedTask
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while handling update");
        }
    }

    private async Task OnMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message is null)
            return;
        switch (message.Text)
        {
            case BotCommands.Start:
                await HandleStartCommandAsync(message.Chat.Id, cancellationToken);
                break;
            case BotCommands.SelectTimezone:
                await HandleSelectTimezoneCommandAsync(message.Chat.Id, cancellationToken);
                break;
            case BotCommands.SelectDays:
                await HandleSelectDaysCommandAsync(message.Chat.Id, cancellationToken);
                break;
            case BotCommands.SelectHours:
                await HandleSelectHoursCommandAsync(message.Chat.Id, cancellationToken);
                break;
            case BotCommands.MySettings:
                await HandleMySettingsCommandAsync(message.Chat.Id, cancellationToken);
                break;
            default:
                return;
        }
    }

    private async Task OnCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        await _botClient!.AnswerCallbackQueryAsync(callbackQuery.Id, "Свершается магия", cancellationToken: cancellationToken);

        var chat = callbackQuery.Message.Chat;
        var callbackData = callbackQuery.Data.Split("--");
        var command = callbackData[0];
        switch (command)
        {
            case MarkupCommands.Timezone:
                await HandleTimezoneCallbackQueryAsync(chat.Id, callbackData[1], cancellationToken);
                break;
            case MarkupCommands.Days:
                await HandleDaysCallbackQueryAsync(chat.Id, callbackData[1], cancellationToken);
                break;
            case MarkupCommands.Hours:
                string[] data = [callbackData[1], callbackData[2]];
                await HandleHoursCallbackQueryAsync(chat.Id, data, cancellationToken);
                break;
        }
    }

    private async Task OnMyChatMemberAsync(ChatMemberUpdated myChatMember, CancellationToken cancellationToken)
    {
        var memberChatId = myChatMember?.Chat.Id;
        if (memberChatId != null)
            await RemoveSubscriberAsync(memberChatId.Value, cancellationToken);
    }

    private async Task HandleStartCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
            return;

        await AddSubscriberAsync(chatId, cancellationToken);

        await SendMessageAsync(chatId, Messages.Message, null, cancellationToken);
    }

    private async Task HandleSelectTimezoneCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var timezonesMarkup = markupService.GetTimezonesMarkup();
        await SendMessageAsync(
            chatId,
            BotCommands.SelectTimezone,
            timezonesMarkup,
            cancellationToken);
    }

    private async Task HandleSelectDaysCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var daysMarkup = markupService.GetDaysMarkup();
        await SendMessageAsync(
            chatId,
            BotCommands.SelectDays,
            daysMarkup,
            cancellationToken);
    }

    private async Task HandleSelectHoursCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var hoursMarkup = markupService.GetHoursMarkup();
        await SendMessageAsync(
            chatId,
            BotCommands.SelectHours,
            hoursMarkup,
            cancellationToken);
    }

    private async Task HandleMySettingsCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var subscriber = _subscribers.First(s => s.ChatId == chatId);
        // todo: find more correct way to find timezone
        var timezones = timezonesService.GetTimezones();
        var timezone = timezones.First(t => t.Offset == subscriber.Offset);

        var info = SettingsHelper.GetSettingsInfo(
            subscriber.StartHourUtc,
            subscriber.EndHourUtc,
            subscriber.Offset,
            timezone.Title,
            subscriber.DaysPerWeek);

        await SendMessageAsync(
            chatId,
            info,
            null,
            cancellationToken
        );
    }

    private async Task HandleTimezoneCallbackQueryAsync(long chatId, string timezoneId, CancellationToken cancellationToken)
    {
        var id = int.Parse(timezoneId);
        var timezone = timezonesService.GetTimezone(id);
        await UpdateSubscriberTimezone(chatId, timezone.Offset, cancellationToken);

        await SendMessageAsync(
            chatId,
            $"Выбрана таймзона: {timezone.Title}",
            null,
            cancellationToken);
    }

    private async Task HandleDaysCallbackQueryAsync(long chatId, string day, CancellationToken cancellationToken)
    {
        var daysPerWeek = int.Parse(day);
        await UpdateSubscriberDaysAsync(chatId, daysPerWeek, cancellationToken);
        await SendMessageAsync(
            chatId,
            $"Ровная спина будет {daysPerWeek} дней в неделю",
            null,
            cancellationToken);
    }

    private async Task HandleHoursCallbackQueryAsync(long chatId, string[] callbackData, CancellationToken cancellationToken)
    {
        if (callbackData[0] == "custom")
        {
            await SendMessageAsync(
                chatId,
                $"Скоро будет. А пока: выпрями спину!",
                null,
                cancellationToken);
            // todo: customize
        }
        else
        {
            var userStartHour = int.Parse(callbackData[0]);
            var userEndHour = int.Parse(callbackData[1]);
            var subscriber = _subscribers.First(x => x.ChatId == chatId);
            var startHourUtc = TimeHelper.GetHourUtc(userStartHour, subscriber.Offset);
            var endHourUtc = TimeHelper.GetHourUtc(userEndHour, subscriber.Offset);
            await UpdateSubscriberHours(chatId, startHourUtc, endHourUtc, cancellationToken);

            await SendMessageAsync(
                chatId,
                $"Выбрано время с {userStartHour} по {userEndHour}",
                null,
                cancellationToken);
        }
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

        var currentOffset = subscriberFromDb.Offset;
        var offsetDiff = Math.Abs(offset - currentOffset);
        var startHourUtc = subscriberFromDb.StartHourUtc;
        var endHourUtc = subscriberFromDb.EndHourUtc;
        if (currentOffset < offset)
        {
            startHourUtc = TimeHelper.RoundHourIfNeed(startHourUtc - offsetDiff);
            endHourUtc = TimeHelper.RoundHourIfNeed(endHourUtc - offsetDiff);
        }
        if (currentOffset > offset)
        {
            startHourUtc = TimeHelper.RoundHourIfNeed(startHourUtc + offsetDiff);
            endHourUtc = TimeHelper.RoundHourIfNeed(endHourUtc + offsetDiff);
        }

        subscriberFromDb.Offset = offset;
        subscriberFromDb.StartHourUtc = startHourUtc;
        subscriberFromDb.EndHourUtc = endHourUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Subscriber's timezone has been updated");

        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
        {
            subscriber.Offset = offset;
            subscriber.StartHourUtc = startHourUtc;
            subscriber.EndHourUtc = endHourUtc;
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