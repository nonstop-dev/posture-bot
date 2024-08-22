using Microsoft.EntityFrameworkCore;
using NonStop.SitUpStraight.Bot.Db;
using NonStop.SitUpStraight.Bot.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace NonStop.SitUpStraight.Bot.Services;

public class SitUpStraightService(
    ILogger<SitUpStraightService> logger,
    IServiceScopeFactory serviceScopeFactory
    ) : BackgroundService, IDisposable
{
    private const string Message = "Выпрями спину!";
    private const int TotalMinutesCount = 60;
    private const int StartHourUtc = 6;
    private const int EndHourUtc = 18;
    private List<Subscriber> _subscribers = [];
    private TelegramBotClient? _botClient;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        InitializeBotClient();
        await RestoreSubscribersAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var minutes = TotalMinutesCount - DateTime.UtcNow.Minute;
            var delay = (minutes * 60) - DateTime.UtcNow.Second;
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

            if (_subscribers.Count == 0)
                continue;

            var currentHour = DateTime.Now.Hour;
            if (currentHour > EndHourUtc || currentHour < StartHourUtc)
                continue;

            var tasks = _subscribers.Select(async subscriber => await SendMessageAsync(subscriber.ChatId, Message, _cancellationTokenSource.Token));
            await Task.WhenAll(tasks);
        }
    }

    private void InitializeBotClient()
    {
        // todo: take from settings
        _botClient = new TelegramBotClient("242464316:AAFxxhWAurba-hw526Uo6TxjO7WS8B7PIio");

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: new() { AllowedUpdates = [] },
            cancellationToken: _cancellationTokenSource.Token
        );

        logger.LogInformation("Bot initialized");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message is null)
        {
            var memberChatId = update.MyChatMember?.Chat.Id;
            if (memberChatId != null)
                await RemoveSubscriberAsync(memberChatId.Value, cancellationToken);
            return;
        }

        if (message.Text is not { })
            return;

        var chatId = message.Chat.Id;
        var subscriber = _subscribers.FirstOrDefault(s => s.ChatId == chatId);
        if (subscriber != null)
            return;

        await AddSubscriberAsync(chatId, cancellationToken);
        await SendMessageAsync(chatId, Message, cancellationToken);
    }

    private async Task SendMessageAsync(long chatId, string message, CancellationToken cancellationToken)
    {
        await _botClient!.SendTextMessageAsync(chatId, Message, cancellationToken: cancellationToken);
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
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        Subscriber newSubscriber = new() { ChatId = chatId };
        _subscribers.Add(newSubscriber);
        dbContext.Subscribers.Add(newSubscriber);
        await dbContext.SaveChangesAsync(cancellationToken);
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
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var subscriberFromDb = await dbContext.Subscribers.FindAsync([chatId, cancellationToken], cancellationToken: cancellationToken);
        if (subscriberFromDb == null)
            return;

        dbContext.Subscribers.Remove(subscriberFromDb);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RestoreSubscribersAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<SitUpStraightDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        _subscribers = await dbContext.Subscribers.ToListAsync(cancellationToken);
        logger.LogInformation("Subscribers have been restored");
    }

    public override void Dispose()
    {
        _cancellationTokenSource.Cancel();
        // GC.SuppressFinalize(this);
    }
}