using Microsoft.EntityFrameworkCore;
using NonStop.SitUpStraight.Bot.Db;
using NonStop.SitUpStraight.Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace NonStop.SitUpStraight.Bot.BackgroundServices;

public class BotPollingService(
    IServiceProvider serviceProvider,
    ITelegramBotClient botClient,
    ILogger<BotPollingService> logger
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseCreatedAndMigratedAsync(stoppingToken);

        try
        {
            await botClient.SetMyDescription("Выровняю спину даже верблюду! 🐫", cancellationToken: stoppingToken);
            var me = await botClient.GetMe(stoppingToken);
            logger.LogInformation("Bot @{BotName} started polling updates", me.Username);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize bot description or get bot info");
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.MyChatMember, UpdateType.CallbackQuery],
            DropPendingUpdates = true
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var updateHandler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();

                await botClient.ReceiveAsync(
                    updateHandler: updateHandler,
                    receiverOptions: receiverOptions,
                    cancellationToken: stoppingToken
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Polling loop encountered an error. Reconnecting in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task EnsureDatabaseCreatedAndMigratedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migration completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run database migrations");
        }
    }
}
