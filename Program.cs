using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

/*
Actions:
+ 1. Send first message after bot starts: "Выпрями спину!"
+ 2. Send the same every hour (by timer). Like 9:00 am, 10:00 am etc.
3. Send only in work days
4. Add settings for bot to schedule sending
5. Add changing the message

Later:
Localization

*/

const string Message = "Выпрями спину!";
int lastHour = DateTime.Now.Hour;
var subscribers = new List<long>();

// todo: take from settings
var botClient = new TelegramBotClient("242464316:AAFxxhWAurba-hw526Uo6TxjO7WS8B7PIio");
CancellationTokenSource cts = new();
botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: new() { AllowedUpdates = [] },
    cancellationToken: cts.Token
);

var _ = Task.Run(async() => {
    var currentHour = DateTime.Now.Hour;
    while (true)
    {
        await Task.Delay(TimeSpan.FromMinutes(15));

        if (lastHour < currentHour || (lastHour == 21 && currentHour == 9))
        {
            lastHour = currentHour;
            
            if (subscribers.Count == 0)
                continue;
            
            var tasks = subscribers.Select(async subscriber => await SendMessageAsync(subscriber, Message, cts.Token));
            await Task.WhenAll(tasks);
        }
    }
});

Console.ReadLine();
cts.Cancel();
return;

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Message is not { } message)
        return;
    if (message.Text is not { } messageText)
        return;
    
    var chatId = message.Chat.Id;
    if (subscribers.Contains(chatId))
        return;
    
    subscribers.Add(chatId);
    await SendMessageAsync(chatId, Message, cancellationToken);
}

async Task SendMessageAsync(long chatId, string message, CancellationToken cancellationToken)
{
    await botClient.SendTextMessageAsync(chatId, Message, cancellationToken: cancellationToken);
}

Task HandlePollingErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken cancellationToken)
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
