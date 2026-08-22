using System.Collections.Concurrent;
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
    IMarkupService markupService,
    IConfiguration configuration
    ) : BackgroundService, IDisposable
{
    private const int TotalMinutesCount = 60;
    private List<Subscriber> _subscribers = [];
    private TelegramBotClient? _botClient;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ConcurrentDictionary<long, FeedbackSession> _feedbackSessions = new();

    private class FeedbackSession
    {
        public int? Rating { get; set; }
        public string? LikedOption { get; set; }
        public string? ImproveOption { get; set; }
        public string? Comment { get; set; }
        public bool WaitingForText { get; set; }
        public string? StepWaitingForText { get; set; }
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseCreatedAndMigrated(stoppingToken);
        await InitializeBotClientAsync(stoppingToken);
        await RestoreSubscribersAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var minutes = TotalMinutesCount - DateTime.UtcNow.Minute;
            var delay = (minutes * 60) - DateTime.UtcNow.Second;
            if (delay <= 0) delay = 60;

            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

            if (_subscribers.Count == 0)
                continue;

            var now = DateTime.UtcNow;
            var currentHourUtc = now.Hour;
            List<(Subscriber Subscriber, string Message)> subscribersWithMessages = [];

            foreach (var s in _subscribers.Where(sub => sub.Configured))
            {
                var dayNumber = now.DayOfWeek.ToDayNumber();
                if (dayNumber > s.DaysPerWeek)
                    continue;

                var startHourUtc = s.StartHourUtc;
                var endHourUtc = s.EndHourUtc;

                bool isWithinHours;
                if (startHourUtc <= endHourUtc)
                {
                    isWithinHours = currentHourUtc >= startHourUtc && currentHourUtc <= endHourUtc;
                }
                else
                {
                    // Ночной интервал через полночь
                    isWithinHours = currentHourUtc >= startHourUtc || currentHourUtc <= endHourUtc;
                }

                if (!isWithinHours)
                    continue;

                var (hourlyMsg, probability) = MessageHelper.GetHourlyMessage(currentHourUtc, startHourUtc, endHourUtc);

                s.TotalMessagesSent++;
                if (probability == MessageProbability.Legend)
                {
                    s.LegendaryCount++;
                }

                // Проверяем юбилейные сообщения (отправляются ВМЕСТО стандартного)
                var milestoneMsg = MessageHelper.GetMilestoneMessage(s.TotalMessagesSent);
                var messageToSend = milestoneMsg ?? hourlyMsg;

                // Проверяем первую или 10-ю легендарку
                if (milestoneMsg == null && probability == MessageProbability.Legend)
                {
                    var specialLegendMsg = MessageHelper.GetLegendaryMilestoneMessage(s.LegendaryCount);
                    if (specialLegendMsg != null)
                    {
                        messageToSend = $"{specialLegendMsg}\n\n{hourlyMsg}";
                    }
                }

                subscribersWithMessages.Add((s, messageToSend));
            }

            if (subscribersWithMessages.Count > 0)
            {
                await SaveSubscribersStatsAsync(subscribersWithMessages.Select(x => x.Subscriber).ToList(), stoppingToken);

                var tasks = subscribersWithMessages.Select(async kv =>
                    await SendMessageAsync(kv.Subscriber.ChatId, kv.Message, markupService.GetDefaultMarkup(), _cancellationTokenSource.Token));

                await Task.WhenAll(tasks);
                logger.LogInformation("Worker: All hourly messages sent to {Count} subscribers", subscribersWithMessages.Count);
            }
        }
    }

    private async Task InitializeBotClientAsync(CancellationToken cancellationToken)
    {
        var botToken = configuration["BotConfiguration:BotToken"]
            ?? throw new InvalidOperationException("Bot token is not configured in BotConfiguration:BotToken");
        _botClient = new TelegramBotClient(botToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.MyChatMember, UpdateType.CallbackQuery],
            DropPendingUpdates = true
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cancellationTokenSource.Token
        );

        await _botClient.SetMyDescription("Выровняю спину даже верблюду! 🐫", cancellationToken: cancellationToken);
        logger.LogInformation("Bot initialized successfully");
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

        var chatId = message.Chat.Id;

        // Обработка геолокации для автоматического определения часового пояса
        if (message.Location != null)
        {
            await HandleLocationAsync(chatId, message.Location, cancellationToken);
            return;
        }

        // Проверяем, находится ли пользователь в процессе ввода текста отзыва
        if (_feedbackSessions.TryGetValue(chatId, out var session) && session.WaitingForText)
        {
            await HandleFeedbackTextInputAsync(chatId, message.Text ?? "", session, cancellationToken);
            return;
        }

        switch (message.Text)
        {
            case BotCommands.Start:
                await HandleStartCommandAsync(chatId, cancellationToken);
                break;
            case BotCommands.Stats or BotCommands.StatsMenu:
                await HandleStatsCommandAsync(chatId, cancellationToken);
                break;
            case BotCommands.Settings or BotCommands.SettingsMenu:
                await HandleSettingsMenuCommandAsync(chatId, cancellationToken);
                break;
            case BotCommands.Feedback or BotCommands.FeedbackMenu:
                await HandleFeedbackCommandAsync(chatId, cancellationToken);
                break;
            case BotCommands.Help or BotCommands.HelpMenu:
                await HandleHelpCommandAsync(chatId, cancellationToken);
                break;
            case BotCommands.SelectTimezone:
                await HandleSelectTimezoneCommandAsync(chatId, cancellationToken);
                break;
            case BotCommands.SelectDays:
                await HandleSelectDaysCommandAsync(chatId, cancellationToken);
                break;
            case BotCommands.SelectHours:
                await HandleSelectHoursCommandAsync(chatId, cancellationToken);
                break;
            case BotCommands.MySettings:
                await HandleMySettingsCommandAsync(chatId, cancellationToken);
                break;
            case "Отмена":
                _feedbackSessions.TryRemove(chatId, out _);
                await SendMessageAsync(chatId, "Действие отменено.", markupService.GetDefaultMarkup(), cancellationToken);
                break;
            default:
                return;
        }
    }

    private async Task OnCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null || string.IsNullOrEmpty(callbackQuery.Data))
            return;

        await _botClient!.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

        var chatId = callbackQuery.Message.Chat.Id;
        var callbackData = callbackQuery.Data.Split("--");
        var command = callbackData[0];

        switch (command)
        {
            case MarkupCommands.StartWizard:
                await HandleSelectTimezoneCommandAsync(chatId, cancellationToken);
                break;
            case "set_tz":
                await HandleSelectTimezoneCommandAsync(chatId, cancellationToken);
                break;
            case "set_days":
                await HandleSelectDaysCommandAsync(chatId, cancellationToken);
                break;
            case "set_hours":
                await HandleSelectHoursCommandAsync(chatId, cancellationToken);
                break;
            case "set_info":
                await HandleMySettingsCommandAsync(chatId, cancellationToken);
                break;
            case MarkupCommands.Timezone:
                if (callbackData[1] == "auto")
                {
                    await SendMessageAsync(
                        chatId,
                        "Нажми кнопку ниже, чтобы отправить геопозицию и автоматически определить часовой пояс:",
                        markupService.GetLocationRequestMarkup(),
                        cancellationToken);
                }
                else
                {
                    await HandleTimezoneCallbackQueryAsync(chatId, callbackData[1], cancellationToken);
                }
                break;
            case MarkupCommands.Days:
                await HandleDaysCallbackQueryAsync(chatId, callbackData[1], cancellationToken);
                break;
            case MarkupCommands.Hours:
                if (callbackData[1] == "custom")
                {
                    await SendMessageAsync(
                        chatId,
                        "⏰ Выбери **час начала** напоминаний (утро):",
                        markupService.GetCustomStartHoursMarkup(),
                        cancellationToken);
                }
                else
                {
                    string[] data = [callbackData[1], callbackData[2]];
                    await HandleHoursCallbackQueryAsync(chatId, data, cancellationToken);
                }
                break;
            case MarkupCommands.CustomHourStart:
                var startHour = int.Parse(callbackData[1]);
                await SendMessageAsync(
                    chatId,
                    $"Час начала выбран: {startHour}:00.\nТеперь выбери **час окончания** напоминаний (вечер):",
                    markupService.GetCustomEndHoursMarkup(startHour),
                    cancellationToken);
                break;
            case MarkupCommands.CustomHourEnd:
                var customStart = int.Parse(callbackData[1]);
                var customEnd = int.Parse(callbackData[2]);
                await HandleHoursCallbackQueryAsync(chatId, [customStart.ToString(), customEnd.ToString()], cancellationToken);
                break;
            case MarkupCommands.FeedbackRating:
                await HandleFeedbackRatingCallbackAsync(chatId, int.Parse(callbackData[1]), cancellationToken);
                break;
            case MarkupCommands.FeedbackLiked:
                await HandleFeedbackLikedCallbackAsync(chatId, callbackData[1], cancellationToken);
                break;
            case MarkupCommands.FeedbackImprove:
                await HandleFeedbackImproveCallbackAsync(chatId, callbackData[1], cancellationToken);
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
        if (subscriber == null)
        {
            await AddSubscriberAsync(chatId, cancellationToken);
        }

        await SendMessageAsync(
            chatId,
            MessageHelper.GetHelloMessage(),
            markupService.GetStartWizardMarkup(),
            cancellationToken);
    }

    private async Task HandleStatsCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber == null)
        {
            await SendMessageAsync(chatId, "Ты пока не зарегистрирован в боте. Нажми /start!", markupService.GetDefaultMarkup(), cancellationToken);
            return;
        }

        var statsText = MessageHelper.GetStatsMessage(subscriber);
        await SendMessageAsync(chatId, statsText, markupService.GetDefaultMarkup(), cancellationToken);
    }

    private async Task HandleHelpCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        await SendMessageAsync(chatId, MessageHelper.GetHelpMessage(), markupService.GetDefaultMarkup(), cancellationToken);
    }

    private async Task HandleSettingsMenuCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        await SendMessageAsync(chatId, "⚙️ **Настройки бота**\nВыбери нужный раздел:", markupService.GetSettingsInlineMarkup(), cancellationToken);
    }

    private async Task HandleSelectTimezoneCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var timezonesMarkup = markupService.GetTimezonesMarkup();
        await SendMessageAsync(chatId, "🌍 Выбери свой город/таймзону:", timezonesMarkup, cancellationToken);
    }

    private async Task HandleSelectDaysCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var daysMarkup = markupService.GetDaysMarkup();
        await SendMessageAsync(chatId, "📅 В какие дни недели присылать напоминания?", daysMarkup, cancellationToken);
    }

    private async Task HandleSelectHoursCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var hoursMarkup = markupService.GetHoursMarkup();
        await SendMessageAsync(chatId, "⏰ В какой интервал времени присылать напоминания?", hoursMarkup, cancellationToken);
    }

    private async Task HandleMySettingsCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber == null)
            return;

        var timezones = timezonesService.GetTimezones();
        var timezone = timezones.FirstOrDefault(t => t.Offset == subscriber.Offset)?.Title ?? $"UTC{(subscriber.Offset >= 0 ? "+" : "")}{subscriber.Offset}";

        var info = SettingsHelper.GetSettingsInfo(
            subscriber.StartHourUtc,
            subscriber.EndHourUtc,
            subscriber.Offset,
            timezone,
            subscriber.DaysPerWeek);

        await SendMessageAsync(chatId, info, markupService.GetDefaultMarkup(), cancellationToken);
    }

    private async Task HandleLocationAsync(long chatId, Location location, CancellationToken cancellationToken)
    {
        int offset = (int)Math.Round(location.Longitude / 15.0);
        offset = Math.Clamp(offset, -12, 14);

        var subscriber = await UpdateSubscriberTimezone(chatId, offset, cancellationToken);
        var markup = IsFirstLaunch(subscriber) ? null : markupService.GetDefaultMarkup();

        await SendMessageAsync(
            chatId,
            $"📍 Часовой пояс определен автоматически: UTC{(offset >= 0 ? "+" : "")}{offset}",
            markup,
            cancellationToken);

        if (IsFirstLaunch(subscriber))
        {
            await HandleSelectDaysCommandAsync(chatId, cancellationToken);
        }
    }

    private async Task HandleTimezoneCallbackQueryAsync(long chatId, string timezoneId, CancellationToken cancellationToken)
    {
        var id = int.Parse(timezoneId);
        var timezone = timezonesService.GetTimezone(id);
        var subscriber = await UpdateSubscriberTimezone(chatId, timezone.Offset, cancellationToken);
        var markup = IsFirstLaunch(subscriber) ? null : markupService.GetDefaultMarkup();

        await SendMessageAsync(
            chatId,
            $"Выбрана таймзона: {timezone.Title}",
            markup,
            cancellationToken);

        if (IsFirstLaunch(subscriber))
        {
            await HandleSelectDaysCommandAsync(chatId, cancellationToken);
        }
    }

    private async Task HandleDaysCallbackQueryAsync(long chatId, string day, CancellationToken cancellationToken)
    {
        var daysPerWeek = int.Parse(day);
        var subscriber = await UpdateSubscriberDaysAsync(chatId, daysPerWeek, cancellationToken);
        var markup = IsFirstLaunch(subscriber) ? null : markupService.GetDefaultMarkup();

        await SendMessageAsync(
            chatId,
            $"Ровная спина будет {daysPerWeek} дней в неделю",
            markup,
            cancellationToken);

        if (IsFirstLaunch(subscriber))
        {
            await HandleSelectHoursCommandAsync(chatId, cancellationToken);
        }
    }

    private async Task HandleHoursCallbackQueryAsync(long chatId, string[] callbackData, CancellationToken cancellationToken)
    {
        var userStartHour = int.Parse(callbackData[0]);
        var userEndHour = int.Parse(callbackData[1]);
        var subscriber = _subscribers.FirstOrDefault(x => x.ChatId == chatId);
        if (subscriber == null) return;

        var startHourUtc = TimeHelper.GetHourUtc(userStartHour, subscriber.Offset);
        var endHourUtc = TimeHelper.GetHourUtc(userEndHour, subscriber.Offset);
        await UpdateSubscriberHours(chatId, startHourUtc, endHourUtc, cancellationToken);

        var markup = IsFirstLaunch(subscriber) ? null : markupService.GetDefaultMarkup();
        await SendMessageAsync(
            chatId,
            $"Выбрано время с {userStartHour}:00 по {userEndHour}:00",
            markup,
            cancellationToken);

        if (IsFirstLaunch(subscriber))
        {
            await UpdateSubscriberConfiguredAsync(chatId, cancellationToken);
            await SendMessageAsync(chatId, MessageHelper.GetConfigurationFinishedMessage(), markupService.GetDefaultMarkup(), cancellationToken);
        }
    }

    // Feedback flow
    private async Task HandleFeedbackCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        _feedbackSessions[chatId] = new FeedbackSession();
        await SendMessageAsync(
            chatId,
            "⭐️ **Как тебе опыт использования бота?**\nОцени от 1 до 5:",
            markupService.GetFeedbackRatingMarkup(),
            cancellationToken);
    }

    private async Task HandleFeedbackRatingCallbackAsync(long chatId, int rating, CancellationToken cancellationToken)
    {
        var session = _feedbackSessions.GetOrAdd(chatId, _ => new FeedbackSession());
        session.Rating = rating;

        await SendMessageAsync(
            chatId,
            "👍 **Что тебе нравится в боте больше всего?**",
            markupService.GetFeedbackLikedMarkup(),
            cancellationToken);
    }

    private async Task HandleFeedbackLikedCallbackAsync(long chatId, string likedOption, CancellationToken cancellationToken)
    {
        var session = _feedbackSessions.GetOrAdd(chatId, _ => new FeedbackSession());
        session.LikedOption = likedOption;

        if (likedOption == "custom")
        {
            session.WaitingForText = true;
            session.StepWaitingForText = "liked";
            await SendMessageAsync(chatId, "✍️ Напиши, что именно тебе нравится в боте:", null, cancellationToken);
            return;
        }

        await SendMessageAsync(
            chatId,
            "💡 **Что нам стоит улучшить или добавить?**",
            markupService.GetFeedbackImproveMarkup(),
            cancellationToken);
    }

    private async Task HandleFeedbackImproveCallbackAsync(long chatId, string improveOption, CancellationToken cancellationToken)
    {
        var session = _feedbackSessions.GetOrAdd(chatId, _ => new FeedbackSession());
        session.ImproveOption = improveOption;

        if (improveOption is "custom" or "suggest")
        {
            session.WaitingForText = true;
            session.StepWaitingForText = "improve";
            var prompt = improveOption == "suggest" ? "📝 Напиши свою фразу для напоминалки:" : "✍️ Напиши свои пожелания или багрепорт:";
            await SendMessageAsync(chatId, prompt, null, cancellationToken);
            return;
        }

        await FinalizeFeedbackAsync(chatId, session, cancellationToken);
    }

    private async Task HandleFeedbackTextInputAsync(long chatId, string text, FeedbackSession session, CancellationToken cancellationToken)
    {
        session.WaitingForText = false;

        if (session.StepWaitingForText == "liked")
        {
            session.LikedOption = text;
            session.StepWaitingForText = null;
            await SendMessageAsync(
                chatId,
                "💡 **Что нам стоит улучшить или добавить?**",
                markupService.GetFeedbackImproveMarkup(),
                cancellationToken);
            return;
        }

        if (session.StepWaitingForText == "improve")
        {
            session.Comment = text;
            session.StepWaitingForText = null;
            await FinalizeFeedbackAsync(chatId, session, cancellationToken);
        }
    }

    private async Task FinalizeFeedbackAsync(long chatId, FeedbackSession session, CancellationToken cancellationToken)
    {
        _feedbackSessions.TryRemove(chatId, out _);

        using (var scope = serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();
            var feedback = new Feedback
            {
                ChatId = chatId,
                Rating = session.Rating,
                LikedOption = session.LikedOption,
                ImproveOption = session.ImproveOption,
                Comment = session.Comment,
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.Feedbacks.Add(feedback);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Пересылка отзыва администраторам, если настроен AdminChatId
        var adminChatIdStr = configuration["BotConfiguration:AdminChatId"];
        if (!string.IsNullOrEmpty(adminChatIdStr) && long.TryParse(adminChatIdStr, out var adminChatId))
        {
            var adminMessage = $"📬 **Новый отзыв о боте!**\n" +
                               $"User ID: `{chatId}`\n" +
                               $"Оценка: {session.Rating} ⭐️\n" +
                               $"Понравилось: {session.LikedOption}\n" +
                               $"Улучшить: {session.ImproveOption}\n" +
                               $"Комментарий: {session.Comment ?? "-"}";
            await SendMessageAsync(adminChatId, adminMessage, null, cancellationToken);
        }

        await SendMessageAsync(
            chatId,
            "✅ **Спасибо за твой отзыв!**\n\nТвоя обратная связь помогает делать бота лучше, а спины — ровнее! 🏔\nПродолжай держать осанку! 💪",
            markupService.GetDefaultMarkup(),
            cancellationToken);
    }

    private async Task SendMessageAsync(long chatId, string message, IReplyMarkup? replyMarkup, CancellationToken cancellationToken)
    {
        try
        {
            await _botClient!.SendMessage(chatId, message, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 403)
        {
            logger.LogWarning("User {ChatId} blocked the bot. Removing subscriber.", chatId);
            await RemoveSubscriberAsync(chatId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send message to {ChatId}", chatId);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken cancellationToken)
    {
        var message = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
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
        logger.LogInformation("Subscriber {ChatId} has been added", chatId);
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

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId], cancellationToken: cancellationToken);
        if (subscriberFromDb == null)
            return;

        dbContext.Subscribers.Remove(subscriberFromDb);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Subscriber {ChatId} has been removed", chatId);
    }

    private async Task RestoreSubscribersAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();
        _subscribers = await dbContext.Subscribers.ToListAsync(cancellationToken);
        logger.LogInformation("Restored {Count} subscribers from DB", _subscribers.Count);
    }

    private async Task EnsureDatabaseCreatedAndMigrated(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migration completed");
    }

    private async Task SaveSubscribersStatsAsync(List<Subscriber> subscribers, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

            var ids = subscribers.Select(s => s.ChatId).ToList();
            var dbSubscribers = await dbContext.Subscribers.Where(s => ids.Contains(s.ChatId)).ToListAsync(cancellationToken);

            foreach (var dbSub in dbSubscribers)
            {
                var local = subscribers.FirstOrDefault(s => s.ChatId == dbSub.ChatId);
                if (local != null)
                {
                    dbSub.TotalMessagesSent = local.TotalMessagesSent;
                    dbSub.LegendaryCount = local.LegendaryCount;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save subscribers stats to DB");
        }
    }

    private async Task<Subscriber?> UpdateSubscriberTimezone(long chatId, int offset, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId], cancellationToken);
        if (subscriberFromDb == null)
            return null;

        var currentOffset = subscriberFromDb.Offset;
        var offsetDiff = Math.Abs(offset - currentOffset);
        var startHourUtc = subscriberFromDb.StartHourUtc;
        var endHourUtc = subscriberFromDb.EndHourUtc;

        if (currentOffset < offset)
        {
            startHourUtc = TimeHelper.RoundHourIfNeed(startHourUtc - offsetDiff);
            endHourUtc = TimeHelper.RoundHourIfNeed(endHourUtc - offsetDiff);
        }
        else if (currentOffset > offset)
        {
            startHourUtc = TimeHelper.RoundHourIfNeed(startHourUtc + offsetDiff);
            endHourUtc = TimeHelper.RoundHourIfNeed(endHourUtc + offsetDiff);
        }

        subscriberFromDb.Offset = offset;
        subscriberFromDb.StartHourUtc = startHourUtc;
        subscriberFromDb.EndHourUtc = endHourUtc;
        await dbContext.SaveChangesAsync(cancellationToken);

        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
        {
            subscriber.Offset = offset;
            subscriber.StartHourUtc = startHourUtc;
            subscriber.EndHourUtc = endHourUtc;
        }

        return subscriber;
    }

    private async Task UpdateSubscriberHours(long chatId, int startHourUtc, int endHourUtc, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId], cancellationToken);
        if (subscriberFromDb == null)
            return;

        subscriberFromDb.StartHourUtc = startHourUtc;
        subscriberFromDb.EndHourUtc = endHourUtc;

        await dbContext.SaveChangesAsync(cancellationToken);

        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
        {
            subscriber.StartHourUtc = startHourUtc;
            subscriber.EndHourUtc = endHourUtc;
        }
    }

    private async Task UpdateSubscriberConfiguredAsync(long chatId, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId], cancellationToken);
        if (subscriberFromDb == null)
            return;

        subscriberFromDb.Configured = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
        {
            subscriber.Configured = true;
        }
    }

    private async Task<Subscriber?> UpdateSubscriberDaysAsync(long chatId, int daysPerWeek, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId], cancellationToken);
        if (subscriberFromDb == null)
            return null;

        subscriberFromDb.DaysPerWeek = daysPerWeek;
        await dbContext.SaveChangesAsync(cancellationToken);

        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
        {
            subscriber.DaysPerWeek = daysPerWeek;
        }

        return subscriber;
    }

    private static bool IsFirstLaunch(Subscriber? subscriber) => subscriber != null && !subscriber.Configured;

    public override void Dispose()
    {
        _cancellationTokenSource.Cancel();
    }
}