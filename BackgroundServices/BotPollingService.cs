using Microsoft.EntityFrameworkCore;
using NonStop.SitUpStraight.Bot.Db;
using NonStop.SitUpStraight.Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace NonStop.SitUpStraight.Bot.BackgroundServices;

public class BotPollingService(
    IServiceScopeFactory scopeFactory,
    ITelegramBotClient botClient,
    UpdateHandler updateHandler,
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
            AllowedUpdates = [],
            DropPendingUpdates = true
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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
        var retryCount = 0;
        const int maxRetries = 15;

        while (retryCount < maxRetries && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migration completed successfully");
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                logger.LogWarning(ex, "Database is not ready yet. Retrying migration ({Retry}/{MaxRetries}) in 2 seconds...", retryCount, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        logger.LogError("Failed to migrate database after {MaxRetries} retries", maxRetries);
    }
}
