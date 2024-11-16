using NonStop.SitUpStraight.Bot.Models;

namespace NonStop.SitUpStraight.Bot.Helpers;

public static class RandomHelper
{
    private static readonly Random _random = new();

    public static MessageProbability GetMessageProbability()
    {
        var value = _random.Next(0, 101);
        var probability = value switch
        {
            > 60 and <= 80 => MessageProbability.Normal,
            > 80 and <= 90 => MessageProbability.Rare,
            > 90 and <= 97 => MessageProbability.Epic,
            > 90 and <= 100 => MessageProbability.Legend,
            _ => MessageProbability.Typical
        };
        return probability;
    }

    public static int GetRandomInt(int min, int max) => _random.Next(min, max);
}