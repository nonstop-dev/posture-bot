using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace NonStop.SitUpStraight.Bot;

public class SitUpStraightService : BackgroundService, IDisposable
{

    private const string Message = "Выпрями спину!";
    private int _lastHour = DateTime.Now.Hour;
    private readonly List<long> _subscribers = [];
    private TelegramBotClient? _botClient;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        InitializeBotClient();
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            var currentHour = DateTime.Now.Hour;

            if (_lastHour < currentHour || _lastHour == 21 && currentHour == 9)
            {
                _lastHour = currentHour;

                if (_subscribers.Count == 0)
                    continue;

                var tasks = _subscribers.Select(async subscriber => await SendMessageAsync(subscriber, Message, _cancellationTokenSource.Token));
                await Task.WhenAll(tasks);
            }
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
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
            return;
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
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        // todo: log error
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _cancellationTokenSource.Cancel();
        base.Dispose();
    }
}