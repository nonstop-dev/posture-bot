using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace NonStop.SitUpStraight.Bot.Services;

public class SitUpStraightService(ILogger<SitUpStraightService> logger) : BackgroundService, IDisposable
{
    private const string Message = "Выпрями спину!";
    private const int TotalMinutesCount = 60;
    private const int StartHourUtc = 6;
    private const int EndHourUtc = 18;
    private readonly List<long> _subscribers = [];
    private TelegramBotClient? _botClient;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        InitializeBotClient();
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

            var tasks = _subscribers.Select(async subscriber => await SendMessageAsync(subscriber, Message, _cancellationTokenSource.Token));
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
                _subscribers.Remove(memberChatId.Value);

            return;
        }

        if (message.Text is not { })
            return;

        var chatId = message.Chat.Id;
        if (_subscribers.Contains(chatId))
            return;

        _subscribers.Add(chatId);
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

    public override void Dispose()
    {
        _cancellationTokenSource.Cancel();
        // GC.SuppressFinalize(this);
    }
}