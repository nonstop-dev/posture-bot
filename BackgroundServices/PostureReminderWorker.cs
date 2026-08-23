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

        var now = DateTime.UtcNow;
        var currentHourUtc = now.Hour;
        var dayNumber = now.DayOfWeek.ToDayNumber();

        List<(Subscriber Subscriber, string Message)> notifications = [];

        foreach (var s in subscribers)
        {
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
