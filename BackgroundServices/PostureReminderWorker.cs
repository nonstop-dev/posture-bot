using Microsoft.EntityFrameworkCore;
using NonStop.Posture.Bot.Db;
using NonStop.Posture.Bot.Extensions;
using NonStop.Posture.Bot.Helpers;
using NonStop.Posture.Bot.Models;
using NonStop.Posture.Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace NonStop.Posture.Bot.BackgroundServices;

public class PostureReminderWorker(
    IServiceProvider serviceProvider,
    ITelegramBotClient botClient,
    IMarkupService markupService,
    ILogger<PostureReminderWorker> logger
    ) : BackgroundService
{
    private const int TotalMinutesCount = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PostureReminderWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var minutes = TotalMinutesCount - DateTime.UtcNow.Minute;
            var delay = (minutes * 60) - DateTime.UtcNow.Second;
            if (delay <= 0) delay = 60;

            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

            try
            {
                await ProcessHourlyRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during hourly reminders processing");
            }
        }
    }

    private async Task ProcessHourlyRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostureDbContext>();

        var subscribers = await dbContext.Subscribers
            .Where(s => s.Configured)
            .ToListAsync(cancellationToken);

        if (subscribers.Count == 0)
            return;

        List<(Subscriber Subscriber, string Message)> notifications = [];

        foreach (var s in subscribers)
        {
            var localNow = DateTime.UtcNow.AddHours(s.Offset);
            var currentHourLocal = localNow.Hour;
            var startHourLocal = TimeHelper.GetHourLocal(s.StartHourUtc, s.Offset);
            var endHourLocal = TimeHelper.GetHourLocal(s.EndHourUtc, s.Offset);

            bool isWithinHours;
            int activeDayNumber;

            if (startHourLocal <= endHourLocal)
            {
                // Дневной интервал в рамках одних суток (например, 09:00 - 21:00 или 00:00 - 04:00)
                isWithinHours = currentHourLocal >= startHourLocal && currentHourLocal <= endHourLocal;
                activeDayNumber = localNow.DayOfWeek.ToDayNumber();
            }
            else
            {
                // Ночной интервал с переходом через полночь (например, 09:00 - 03:00 или 22:00 - 04:00)
                if (currentHourLocal >= startHourLocal)
                {
                    // До полуночи: смена сегодняшнего дня
                    isWithinHours = true;
                    activeDayNumber = localNow.DayOfWeek.ToDayNumber();
                }
                else if (currentHourLocal <= endHourLocal)
                {
                    // После полуночи: продолжение смены вчерашнего дня
                    isWithinHours = true;
                    activeDayNumber = localNow.AddDays(-1).DayOfWeek.ToDayNumber();
                }
                else
                {
                    isWithinHours = false;
                    activeDayNumber = localNow.DayOfWeek.ToDayNumber();
                }
            }

            if (!isWithinHours || activeDayNumber > s.DaysPerWeek)
                continue;

            var (hourlyMsg, probability) = MessageHelper.GetHourlyMessage(currentHourLocal, startHourLocal, endHourLocal);

            s.TotalMessagesSent++;
            if (probability == MessageProbability.Legend)
            {
                s.LegendaryCount++;
            }

            // Юбилейные карточки заменяют стандартное сообщение
            var milestoneMsg = MessageHelper.GetMilestoneMessage(s.TotalMessagesSent);
            var messageToSend = milestoneMsg ?? hourlyMsg;

            // Специальные события на 1-ю и 10-ю легендарку
            if (milestoneMsg == null && probability == MessageProbability.Legend)
            {
                var specialLegendMsg = MessageHelper.GetLegendaryMilestoneMessage(s.LegendaryCount);
                if (specialLegendMsg != null)
                {
                    messageToSend = $"{specialLegendMsg}\n\n{hourlyMsg}";
                }
            }

            notifications.Add((s, messageToSend));
        }

        if (notifications.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            var sendTasks = notifications.Select(async item =>
            {
                try
                {
                    await botClient.SendMessage(
                        item.Subscriber.ChatId,
                        item.Message,
                        parseMode: ParseMode.Html,
                        replyMarkup: markupService.GetDefaultMarkup(),
                        cancellationToken: cancellationToken);
                }
                catch (ApiRequestException ex) when (ex.ErrorCode == 403)
                {
                    logger.LogWarning("User {ChatId} blocked the bot during reminder. Removing subscriber.", item.Subscriber.ChatId);
                    using var innerScope = serviceProvider.CreateScope();
                    var innerDb = innerScope.ServiceProvider.GetRequiredService<PostureDbContext>();
                    var toRemove = await innerDb.Subscribers.FindAsync([item.Subscriber.ChatId], cancellationToken);
                    if (toRemove != null)
                    {
                        innerDb.Subscribers.Remove(toRemove);
                        await innerDb.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send reminder to {ChatId}", item.Subscriber.ChatId);
                }
            });

            await Task.WhenAll(sendTasks);
            logger.LogInformation("Hourly reminders sent to {Count} subscribers", notifications.Count);
        }
    }
}
