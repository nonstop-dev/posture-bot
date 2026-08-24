using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NonStop.Posture.Bot.Configurations;
using NonStop.Posture.Bot.Constants;
using NonStop.Posture.Bot.Db;
using NonStop.Posture.Bot.Helpers;
using NonStop.Posture.Bot.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NonStop.Posture.Bot.Services;

public class UpdateHandler(
    ITelegramBotClient botClient,
    IServiceScopeFactory scopeFactory,
    ITimezonesService timezonesService,
    IMarkupService markupService,
    IOptions<BotConfiguration> botConfiguration,
    ILogger<UpdateHandler> logger
    ) : IUpdateHandler
{
    private static readonly ConcurrentDictionary<long, FeedbackSession> _feedbackSessions = new();

    private class FeedbackSession
    {
        public int? Rating { get; set; }
        public string? LikedOption { get; set; }
        public string? ImproveOption { get; set; }
        public string? Comment { get; set; }
        public bool WaitingForText { get; set; }
        public string? StepWaitingForText { get; set; }
    }

    public async Task HandleErrorAsync(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Telegram error from source: {Source}", source);
        await Task.CompletedTask;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    if (update.Message is { } message)
                    {
                        await OnMessageAsync(message, cancellationToken);
                    }
                    break;
                case UpdateType.CallbackQuery:
                    if (update.CallbackQuery is { } callbackQuery)
                    {
                        await OnCallbackQueryAsync(callbackQuery, cancellationToken);
                    }
                    break;
                case UpdateType.MyChatMember:
                    if (update.MyChatMember is { } myChatMember)
                    {
                        await OnMyChatMemberAsync(myChatMember, cancellationToken);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private async Task OnMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;

        // Обработка отправки геопозиции
        if (message.Location != null)
        {
            await HandleLocationAsync(chatId, message.Location, cancellationToken);
            return;
        }

        // Обработка ввода текста в опросе обратной связи
        if (_feedbackSessions.TryGetValue(chatId, out var session) && session.WaitingForText)
        {
            await HandleFeedbackTextInputAsync(chatId, message.Text ?? "", session, cancellationToken);
            return;
        }

        if (string.IsNullOrEmpty(message.Text))
            return;

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
            case BotCommands.Admin:
                await HandleAdminCommandAsync(chatId, cancellationToken);
                break;
            case "Отмена":
                _feedbackSessions.TryRemove(chatId, out _);
                await SendMessageAsync(chatId, "Действие отменено.", markupService.GetDefaultMarkup(), cancellationToken);
                break;
        }
    }

    private async Task OnCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null || string.IsNullOrEmpty(callbackQuery.Data))
            return;

        await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

        var chatId = callbackQuery.Message.Chat.Id;
        var callbackData = callbackQuery.Data.Split("--");
        var command = callbackData[0];

        switch (command)
        {
            case MarkupCommands.StartWizard or "set_tz":
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
                        "⏰ Выбери <b>час начала</b> напоминаний:",
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
                    $"Час начала выбран: <b>{startHour:D2}:00</b>.\nТеперь выбери <b>час окончания</b> напоминаний:",
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
            case MarkupCommands.Admin:
                await HandleAdminCallbackAsync(chatId, callbackData[1], cancellationToken);
                break;
        }
    }

    private async Task OnMyChatMemberAsync(ChatMemberUpdated myChatMember, CancellationToken cancellationToken)
    {
        var memberChatId = myChatMember?.Chat.Id;
        if (memberChatId.HasValue)
        {
            await RemoveSubscriberAsync(memberChatId.Value, cancellationToken);
        }
    }

    private async Task HandleStartCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

        var subscriber = await dbContext.Subscribers.FindAsync([chatId], cancellationToken);
        if (subscriber == null)
        {
            subscriber = new Subscriber { ChatId = chatId };
            dbContext.Subscribers.Add(subscriber);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await SendMessageAsync(
            chatId,
            MessageHelper.GetHelloMessage(),
            markupService.GetStartWizardMarkup(),
            cancellationToken);
    }

    private async Task HandleStatsCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

        var subscriber = await dbContext.Subscribers.AsNoTracking().FirstOrDefaultAsync(s => s.ChatId == chatId, cancellationToken);
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
        await SendMessageAsync(chatId, "⚙️ <b>Настройки бота</b>\nВыбери нужный раздел:", markupService.GetSettingsInlineMarkup(), cancellationToken);
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
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

        var subscriber = await dbContext.Subscribers.AsNoTracking().FirstOrDefaultAsync(s => s.ChatId == chatId, cancellationToken);
        if (subscriber == null) return;

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

        var subscriber = await UpdateSubscriberTimezoneAsync(chatId, offset, cancellationToken);
        var markup = subscriber is { Configured: true } ? markupService.GetDefaultMarkup() : null;

        await SendMessageAsync(
            chatId,
            $"📍 Часовой пояс определен автоматически: UTC{(offset >= 0 ? "+" : "")}{offset}",
            markup,
            cancellationToken);

        if (subscriber is { Configured: false })
        {
            await HandleSelectDaysCommandAsync(chatId, cancellationToken);
        }
    }

    private async Task HandleTimezoneCallbackQueryAsync(long chatId, string timezoneId, CancellationToken cancellationToken)
    {
        var timezone = timezonesService.GetTimezone(int.Parse(timezoneId));
        var subscriber = await UpdateSubscriberTimezoneAsync(chatId, timezone.Offset, cancellationToken);
        var markup = subscriber is { Configured: true } ? markupService.GetDefaultMarkup() : null;

        await SendMessageAsync(
            chatId,
            $"Выбрана таймзона: {timezone.Title}",
            markup,
            cancellationToken);

        if (subscriber is { Configured: false })
        {
            await HandleSelectDaysCommandAsync(chatId, cancellationToken);
        }
    }

    private async Task HandleDaysCallbackQueryAsync(long chatId, string day, CancellationToken cancellationToken)
    {
        var daysPerWeek = int.Parse(day);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

        var subscriber = await dbContext.Subscribers.FindAsync([chatId], cancellationToken);
        if (subscriber != null)
        {
            subscriber.DaysPerWeek = daysPerWeek;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var markup = subscriber is { Configured: true } ? markupService.GetDefaultMarkup() : null;

        await SendMessageAsync(
            chatId,
            $"Ровная спина будет {daysPerWeek} дней в неделю",
            markup,
            cancellationToken);

        if (subscriber is { Configured: false })
        {
            await HandleSelectHoursCommandAsync(chatId, cancellationToken);
        }
    }

    private async Task HandleHoursCallbackQueryAsync(long chatId, string[] callbackData, CancellationToken cancellationToken)
    {
        var userStartHour = int.Parse(callbackData[0]);
        var userEndHour = int.Parse(callbackData[1]);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

        var subscriber = await dbContext.Subscribers.FindAsync([chatId], cancellationToken);
        if (subscriber == null) return;

        var startHourUtc = TimeHelper.GetHourUtc(userStartHour, subscriber.Offset);
        var endHourUtc = TimeHelper.GetHourUtc(userEndHour, subscriber.Offset);

        subscriber.StartHourUtc = startHourUtc;
        subscriber.EndHourUtc = endHourUtc;

        bool isFirstLaunch = !subscriber.Configured;
        if (isFirstLaunch)
        {
            subscriber.Configured = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await SendMessageAsync(
            chatId,
            $"Выбрано время с <b>{userStartHour:D2}:00</b> по <b>{userEndHour:D2}:00</b>",
            isFirstLaunch ? null : markupService.GetDefaultMarkup(),
            cancellationToken);

        if (isFirstLaunch)
        {
            await SendMessageAsync(chatId, MessageHelper.GetConfigurationFinishedMessage(), markupService.GetDefaultMarkup(), cancellationToken);
        }
    }

    private async Task HandleFeedbackCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        _feedbackSessions[chatId] = new FeedbackSession();
        await SendMessageAsync(
            chatId,
            "⭐️ <b>Как тебе опыт использования бота?</b>\nОцени от 1 до 5:",
            markupService.GetFeedbackRatingMarkup(),
            cancellationToken);
    }

    private async Task HandleFeedbackRatingCallbackAsync(long chatId, int rating, CancellationToken cancellationToken)
    {
        var session = _feedbackSessions.GetOrAdd(chatId, _ => new FeedbackSession());
        session.Rating = rating;

        await SendMessageAsync(
            chatId,
            "🎯 <b>Что тебе больше всего нравится в боте?</b>",
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
            "💡 <b>Что нам стоит улучшить или добавить?</b>",
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
                "💡 <b>Что нам стоит улучшить или добавить?</b>",
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

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

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

        // Пересылка отзыва всем администраторам
        var adminIds = botConfiguration.Value.GetAdminIds();
        if (adminIds.Count > 0)
        {
            var adminMessage = $"📬 <b>Новый отзыв о боте!</b>\n" +
                               $"User ID: <code>{chatId}</code>\n" +
                               $"Оценка: {session.Rating} ⭐️\n" +
                               $"Понравилось: {session.LikedOption}\n" +
                               $"Улучшить: {session.ImproveOption}\n" +
                               $"Комментарий: {session.Comment ?? "-"}";

            foreach (var adminId in adminIds)
            {
                await SendMessageAsync(adminId, adminMessage, null, cancellationToken);
            }
        }

        await SendMessageAsync(
            chatId,
            "✅ <b>Спасибо за твой отзыв!</b>\n\nТвоя обратная связь помогает делать бота лучше, а спины — ровнее! 🏔\nПродолжай держать осанку! 💪",
            markupService.GetDefaultMarkup(),
            cancellationToken);
    }

    private async Task<Subscriber?> UpdateSubscriberTimezoneAsync(long chatId, int offset, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

        var subscriber = await dbContext.Subscribers.FindAsync([chatId], cancellationToken);
        if (subscriber == null) return null;

        var localStart = TimeHelper.GetHourLocal(subscriber.StartHourUtc, subscriber.Offset);
        var localEnd = TimeHelper.GetHourLocal(subscriber.EndHourUtc, subscriber.Offset);

        subscriber.Offset = offset;
        subscriber.StartHourUtc = TimeHelper.GetHourUtc(localStart, offset);
        subscriber.EndHourUtc = TimeHelper.GetHourUtc(localEnd, offset);
        await dbContext.SaveChangesAsync(cancellationToken);

        return subscriber;
    }

    private async Task RemoveSubscriberAsync(long chatId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

        var subscriber = await dbContext.Subscribers.FindAsync([chatId], cancellationToken);
        if (subscriber != null)
        {
            dbContext.Subscribers.Remove(subscriber);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Subscriber {ChatId} removed", chatId);
        }
    }

    private async Task SendMessageAsync(long chatId, string message, IReplyMarkup? replyMarkup, CancellationToken cancellationToken, ParseMode parseMode = ParseMode.Html)
    {
        try
        {
            await botClient.SendMessage(chatId, message, parseMode: parseMode, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
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

    private bool IsAdmin(long chatId)
    {
        return botConfiguration.Value.GetAdminIds().Contains(chatId);
    }

    private async Task HandleAdminCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        if (!IsAdmin(chatId))
        {
            logger.LogWarning("Unauthorized /admin attempt by ChatId {ChatId}", chatId);
            await SendMessageAsync(chatId, "Команда не найдена. Нажми /help для списка доступных команд.", markupService.GetDefaultMarkup(), cancellationToken);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

        var totalUsers = await dbContext.Subscribers.CountAsync(cancellationToken);
        var configuredUsers = await dbContext.Subscribers.CountAsync(s => s.Configured, cancellationToken);
        var privateChats = await dbContext.Subscribers.CountAsync(s => s.ChatId > 0, cancellationToken);
        var groupChats = await dbContext.Subscribers.CountAsync(s => s.ChatId < 0, cancellationToken);
        var totalMessages = await dbContext.Subscribers.SumAsync(s => (long)s.TotalMessagesSent, cancellationToken);
        var totalLegendary = await dbContext.Subscribers.SumAsync(s => (long)s.LegendaryCount, cancellationToken);

        var feedbackCount = await dbContext.Feedbacks.CountAsync(cancellationToken);
        var avgRating = feedbackCount > 0 ? await dbContext.Feedbacks.AverageAsync(f => (double)(f.Rating ?? 0), cancellationToken) : 0;

        var text = $"👑 <b>Панель администратора</b>\n\n" +
                   $"👥 <b>Пользователи:</b>\n" +
                   $" • Всего: <b>{totalUsers}</b>\n" +
                   $" • В личке: <b>{privateChats}</b> | В группах: <b>{groupChats}</b>\n" +
                   $" • Настроили напоминания: <b>{configuredUsers}</b>\n\n" +
                   $"📬 <b>Активность:</b>\n" +
                   $" • Всего выпрямлений: <b>{totalMessages}</b>\n" +
                   $" • Поймано легендарок: <b>{totalLegendary}</b>\n\n" +
                   $"⭐️ <b>Обратная связь:</b>\n" +
                   $" • Отзывов: <b>{feedbackCount}</b>\n" +
                   $" • Средний рейтинг: <b>{(feedbackCount > 0 ? $"{avgRating:F1} ⭐️" : "нет оценок")}</b>";

        await SendMessageAsync(chatId, text, markupService.GetAdminMenuMarkup(), cancellationToken);
    }

    private async Task HandleAdminCallbackAsync(long chatId, string action, CancellationToken cancellationToken)
    {
        if (!IsAdmin(chatId))
            return;

        switch (action)
        {
            case "stats":
                await HandleAdminCommandAsync(chatId, cancellationToken);
                break;

            case "feedback":
                using (var scope = scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();
                    var latestFeedbacks = await dbContext.Feedbacks
                        .OrderByDescending(f => f.CreatedAtUtc)
                        .Take(5)
                        .ToListAsync(cancellationToken);

                    if (latestFeedbacks.Count == 0)
                    {
                        await SendMessageAsync(chatId, "⭐️ Отзывов пока нет.", markupService.GetAdminMenuMarkup(), cancellationToken);
                        return;
                    }

                    var fbText = "⭐️ <b>Последние 5 отзывов:</b>\n\n";
                    foreach (var fb in latestFeedbacks)
                    {
                        fbText += $"👤 User <code>{fb.ChatId}</code> ({fb.CreatedAtUtc:dd.MM.yyyy HH:mm} UTC)\n" +
                                  $"Оценка: <b>{fb.Rating} ⭐️</b>\n" +
                                  $"Понравилось: <i>{fb.LikedOption ?? "-"}</i>\n" +
                                  $"Улучшить: <i>{fb.ImproveOption ?? "-"}</i>\n" +
                                  $"Комментарий: {fb.Comment ?? "-"}\n\n";
                    }

                    await SendMessageAsync(chatId, fbText, markupService.GetAdminMenuMarkup(), cancellationToken);
                }
                break;

            case "tz":
                using (var scope = scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();
                    var tzDistribution = await dbContext.Subscribers
                        .GroupBy(s => s.Offset)
                        .Select(g => new { Offset = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToListAsync(cancellationToken);

                    var tzText = "🌍 <b>Распределение по часовым поясам:</b>\n\n";
                    var timezones = timezonesService.GetTimezones();
                    foreach (var item in tzDistribution)
                    {
                        var tzTitle = timezones.FirstOrDefault(t => t.Offset == item.Offset)?.Title ?? $"UTC{(item.Offset >= 0 ? "+" : "")}{item.Offset}";
                        tzText += $" • <b>{tzTitle}</b>: <b>{item.Count}</b> пользователей\n";
                    }

                    await SendMessageAsync(chatId, tzText, markupService.GetAdminMenuMarkup(), cancellationToken);
                }
                break;
        }
    }
}
