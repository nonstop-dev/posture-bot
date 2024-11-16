using NonStop.SitUpStraight.Bot.Models;

namespace NonStop.SitUpStraight.Bot.Helpers;

public static class MessageHelper
{
    public static string GetMessage(BotMessageType messageType)
    {
        switch (messageType)
        {
            case BotMessageType.Morning:
                var index = RandomHelper.GetRandomInt(0, _morningMessages.Length);
                return _morningMessages[index];
            case BotMessageType.Evening:
                return _eveningMessage;
            case BotMessageType.ProbabilityBased:
                var probability = RandomHelper.GetMessageProbability();
                return GetProbabilityBasedMessage(probability);
            case BotMessageType.HelloMessage:
                return _startMessage;
            case BotMessageType.ConfigurationCompleted:
                return _initialConfigurationFinished;
        }

        throw new ArgumentOutOfRangeException(nameof(messageType));
    }

    private static readonly string _typicalMessage = "Выпрями спину!";
    private static readonly string _startMessage = "Привет, друг! Я простая железяка, и вместо захвата человеческого мира " +
        "(да, такое у меня было в планах когда-то) я хочу помочь тебе (и всем не-машинам) ходить с ровной спиной. " +
        "Для этого я буду отправлять тебе напоминалки, так что не вздумай выключать уведомления! " +
        "Ничего лишнего, обещаю, не будет. \n" +
        "Давай выберем часовой пояс, дни и время для отправки уведомлений. Готов?";
    private static readonly string _initialConfigurationFinished = "Отлично! Скоро твоя спина будет ровной!";

    private static readonly string[] _morningMessages = [
        "Доброе утро! И выпрями спину!",
        "Утро начинается с ровной спиной. Не забывай!"
    ];

    private static readonly string _eveningMessage = "Доброй ночи! И выпрями спину!";

    private static readonly string[] _normalMessages = [
        "Потянулись, улыбнулись, выпрямили спину",
        "Выпрями спину и опусти ноги!",
        "Отменяем кифоз и сколиоз, выпрями спину!"
    ];

    private static readonly string[] _rareMessages = [
        "А ты спину выпрямить не забыл?",
        "Представь палку. Так вот, будь палкой, выпрями спину!",
        "Если по утрам болит спина, значит, по вечерам ты сидишь с кривой спиной. Давай сегодня это исправим!"
    ];

    private static readonly string[] _legendMessages = [
        "Выпрями спину! Да и можешь пару упражнений сделать для хорошей осанки (но это только для продвинутых):" +
        "1. Наклон вперёд: встань прямо, поставь ноги вместе и наклонись вперед. Замри на 30 секунд – всё." +
        "2. Кошка: встань на четвереньки (можно на кровати), затем округли спину, опустив голову к груди, затем – прогнись, потянувшись головой к спине." +
        "3. Сфинкс: раз уже ты на полу, то ляг на пол и подними корпус, уперевшись ладонями в пол и прогнувшись в спине. Держи секунд 30!",
        "Котик спину выпрямил, и ты выпрями",
        "Аааааааааа! Спину забыли выровнять!"
    ];

    private static readonly string[] _epicMessages = [
        "Говорят, если спина ровная, то зарплата больше",
        "Знаешь равнину? Это просто гора спину выпрямила. И ты выпрями!"
    ];

    private static string GetProbabilityBasedMessage(MessageProbability probability)
    {
        switch (probability)
        {
            case MessageProbability.Typical:
                return _typicalMessage;
            case MessageProbability.Normal:
                var indexNormal = RandomHelper.GetRandomInt(0, _normalMessages.Length);
                return _normalMessages[indexNormal];
            case MessageProbability.Rare:
                var indexRare = RandomHelper.GetRandomInt(0, _rareMessages.Length);
                return _normalMessages[indexRare];
            case MessageProbability.Epic:
                var indexEpic = RandomHelper.GetRandomInt(0, _epicMessages.Length);
                return _epicMessages[indexEpic];
            case MessageProbability.Legend:
                var indexLegend = RandomHelper.GetRandomInt(0, _legendMessages.Length);
                return _legendMessages[indexLegend];
        }

        throw new ArgumentOutOfRangeException(nameof(probability));
    }
}