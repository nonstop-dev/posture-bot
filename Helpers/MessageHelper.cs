using NonStop.SitUpStraight.Bot.Models;

namespace NonStop.SitUpStraight.Bot.Helpers;

public static class MessageHelper
{
    private static readonly string _startMessage = "Привет, друг! Я простая железяка, и вместо захвата человеческого мира " +
        "(да, такое у меня было в планах когда-то) я хочу помочь тебе (и всем не-машинам) ходить с ровной спиной. " +
        "Для этого я буду отправлять тебе напоминалки, так что не вздумай выключать уведомления! " +
        "Ничего лишнего, обещаю, не будет.\n\n" +
        "Давай выберем часовой пояс, дни и время для отправки уведомлений. Готов?";

    private static readonly string _initialConfigurationFinished = "✅ Всё готово!\n" +
        "Я буду напоминать тебе держать осанку по твоему расписанию.\n\n" +
        "А пока — выпрями спину и опусти плечи! 💪";

    // Утренние сообщения
    private static readonly string[] _morningTypicalMessages = [
        "Доброе утро! И выпрями спину!",
        "Утро начинается с ровной спиной. Не забывай!",
        "Новый день — прямая осанка!"
    ];

    private static readonly string[] _morningRareMessages = [
        "Если по утрам болит спина, значит, по вечерам ты сидишь с кривой спиной. Давай сегодня это исправим!",
        "Утренний кофе бодрит, а прямая спина добавляет +100 к продуктивности. Выпрямись!"
    ];

    private static readonly string[] _morningNormalMessages = [
        "Потянулись, улыбнулись, выпрямили спину!",
        "Время просыпаться, потягиваться и расправлять плечи!"
    ];

    private static readonly string[] _morningEpicMessages = [
        "Солнце встало, горы расправились, и ты выпрями спину!"
    ];

    private static readonly string[] _morningLegendMessages = [
        "Утренняя мини-зарядка для спины (30 секунд):\n1. Сцепи руки в замок за спиной и потянись грудью вперед.\n2. Сделай 5 глубоких вдохов.\n3. Опусти плечи. Готово, ты великолепно держишься!"
    ];

    // Дневные сообщения
    private static readonly string _dayTypicalMessage = "Выпрями спину!";

    private static readonly string[] _dayRareMessages = [
        "А ты спину выпрямить не забываешь?",
        "Представь палку. Так вот, будь палкой, выпрями спину!",
        "Твой позвоночник передает привет и очень просит не складываться пополам.",
        "Представь, что невидимая ниточка тянет за макушку вверх. Выпрямись!"
    ];

    private static readonly string[] _dayNormalMessages = [
        "Выпрями спину и опусти ноги!",
        "Отменяем кифоз и сколиоз, выпрями спину!",
        "Спину держи ровно!",
        "Проверка осанки: выпрямись прямо сейчас!",
        "Плечи назад, спину прямо!",
        "Хватит стекать по креслу, выпрями спину!",
        "Перестань косплеить креветку, сядь прямо!"
    ];

    private static readonly string[] _dayEpicMessages = [
        "Говорят, если спина ровная, то зарплата больше.",
        "Знаешь равнину? Это просто гора спину выпрямила. И ты выпрями!",
        "Выпрями спину, гора!",
        "Ученые доказали: прямая спина отпугивает дедлайны и привлекает удачу."
    ];

    private static readonly string[] _dayLegendMessages = [
        "Выпрями спину! Да и можно пару упражнений сделать для хорошей осанки (только для продвинутых):\n" +
        "1. Наклон вперёд: встань прямо, поставь ноги вместе и наклонись вперед. Замри на 30 секунд – всё.\n" +
        "2. Кошка: встань на четвереньки (можно на кровати), затем округли спину, опустив голову к груди, затем – прогнись, потянувшись головой к спине.\n" +
        "3. Сфинкс: раз уже на полу, то ляг на живот и подними корпус, уперевшись ладонями в пол и прогнувшись в спине. Держи секунд 30!",
        "Котик спину выпрямил, и ты выпрями!",
        "Аааааааааа! Спину забыли выровнять!",
        "Экстренное выравнивание: оторви взгляд от экрана, сведи лопатки вместе на 10 секунд и выдохни. Твоя спина скажет спасибо!"
    ];

    // Вечерние сообщения
    private static readonly string[] _eveningTypicalMessages = [
        "Доброй ночи! И выпрями спину!",
        "День позади. Выпрями спину перед сном и отлично отдохни!"
    ];

    private static readonly string[] _eveningRareMessages = [
        "Финальная проверка осанки на сегодня! Завтра продолжим держать планку."
    ];

    private static readonly string[] _eveningNormalMessages = [
        "Спина сегодня славно потрудилась. Расправь плечи и приятного вечера!"
    ];

    private static readonly string[] _eveningEpicLegendMessages = [
        "Целый день с ровной спиной! Легенда! 👑"
    ];

    public static string GetHelloMessage() => _startMessage;

    public static string GetConfigurationFinishedMessage() => _initialConfigurationFinished;

    public static (string Message, MessageProbability Probability) GetHourlyMessage(int currentHourUtc, int startHourUtc, int endHourUtc)
    {
        var probability = RandomHelper.GetMessageProbability();

        if (currentHourUtc == startHourUtc)
        {
            return (GetMorningMessage(probability), probability);
        }

        if (currentHourUtc == endHourUtc)
        {
            return (GetEveningMessage(probability), probability);
        }

        return (GetDayMessage(probability), probability);
    }

    public static string GetMorningMessage(MessageProbability probability) => probability switch
    {
        MessageProbability.Typical => _morningTypicalMessages[RandomHelper.GetRandomInt(0, _morningTypicalMessages.Length)],
        MessageProbability.Rare => _morningRareMessages[RandomHelper.GetRandomInt(0, _morningRareMessages.Length)],
        MessageProbability.Normal => _morningNormalMessages[RandomHelper.GetRandomInt(0, _morningNormalMessages.Length)],
        MessageProbability.Epic => _morningEpicMessages[RandomHelper.GetRandomInt(0, _morningEpicMessages.Length)],
        MessageProbability.Legend => _morningLegendMessages[RandomHelper.GetRandomInt(0, _morningLegendMessages.Length)],
        _ => _morningTypicalMessages[0]
    };

    public static string GetDayMessage(MessageProbability probability) => probability switch
    {
        MessageProbability.Typical => _dayTypicalMessage,
        MessageProbability.Rare => _dayRareMessages[RandomHelper.GetRandomInt(0, _dayRareMessages.Length)],
        MessageProbability.Normal => _dayNormalMessages[RandomHelper.GetRandomInt(0, _dayNormalMessages.Length)],
        MessageProbability.Epic => _dayEpicMessages[RandomHelper.GetRandomInt(0, _dayEpicMessages.Length)],
        MessageProbability.Legend => _dayLegendMessages[RandomHelper.GetRandomInt(0, _dayLegendMessages.Length)],
        _ => _dayTypicalMessage
    };

    public static string GetEveningMessage(MessageProbability probability) => probability switch
    {
        MessageProbability.Typical => _eveningTypicalMessages[RandomHelper.GetRandomInt(0, _eveningTypicalMessages.Length)],
        MessageProbability.Rare => _eveningRareMessages[RandomHelper.GetRandomInt(0, _eveningRareMessages.Length)],
        MessageProbability.Normal => _eveningNormalMessages[RandomHelper.GetRandomInt(0, _eveningNormalMessages.Length)],
        MessageProbability.Epic or MessageProbability.Legend => _eveningEpicLegendMessages[RandomHelper.GetRandomInt(0, _eveningEpicLegendMessages.Length)],
        _ => _eveningTypicalMessages[0]
    };

    public static string? GetMilestoneMessage(int newTotalCount) => newTotalCount switch
    {
        50 => "🥉 50-е выпрямление! Твой ранг повышен: теперь ты 🪵 Палка! Привычка начинает формироваться, держись прямо!",
        100 => "🥈 100-е выпрямление! Твой ранг повышен: 🐈 Выпрямившийся котик! Позвоночник шлет тебе воздушный поцелуй. Выпрямись!",
        250 => "🥇 250-е выпрямление! Твой ранг повышен: 🏛 Античная колонна! Осанка входит в железную привычку. Выпрями спину!",
        500 => "🦾 Привет, это Настя из команды создателей бота “Выпрями спину!”. Ого-го, на твоем счету уже 500 ровных спин, и твой ранг — Стальной хребет! Скажу по секрету, я терпеть этого бота не могу, честно! Он заставляет меня выпрямлять спину! Однако моя спина говорит ему спасибо! И ты выпрями спину!",
        1000 => "⚡ Привет, это Андрей из команды создателей бота “Выпрями спину!”. Твоя спина выровнялась уже 1000 раз, и ты теперь — Титан осанки! Вот это да! Продолжай! Кривая спина — это не круто :) поэтому: выпрями спину!",
        2000 => "🌅 2 000-е выпрямление! Поздравляем, ты достиг вершины эволюции осанки и получил ранг Великая Равнина! Та самая гора, которая полностью выпрямила спину. Мы гордимся тобой!",
        _ => null
    };

    public static string? GetLegendaryMilestoneMessage(int legendaryCount) => legendaryCount switch
    {
        1 => "🌟 Первая легенда! Тебе выпало легендарное напоминание (шанс всего 3%). Запомни этот момент и выпрямись!",
        10 => "✨ Ты – мега легенда! На твоём счету уже 10 пойманных легендарных напоминалок!",
        _ => null
    };

    public static string GetStatsMessage(Subscriber subscriber)
    {
        var rank = RankHelper.GetRank(subscriber.TotalMessagesSent);
        return $"📬 Выпрямлений: {subscriber.TotalMessagesSent}\n" +
               $"🌟 Поймано легендарок: {subscriber.LegendaryCount} / 10\n" +
               $"🎖 Ранг: {rank}\n\n" +
               $"Расправь плечи и держи планку! 💪";
    }

    public static string GetHelpMessage() =>
        "ℹ️ «Выпрями спину, гора!» — бот для твоей идеальной осанки.\n\n" +
        "Команды:\n" +
        "📊 /stats — Твой текущий ранг, счетчик выпрямлений и легендарок\n" +
        "⚙️ /settings — Настройка часового пояса, дней и часов напоминаний\n" +
        "✍️ /feedback — Оставить отзыв или предложить новую фразу\n" +
        "ℹ️ /help — Эта справка\n\n" +
        "Выровняю спину даже верблюду! 🐫";
}