using NonStop.Posture.Bot.Models;

namespace NonStop.Posture.Bot.Helpers;

public static class RandomHelper
{
    private static readonly Random _random = new();

    public static MessageProbability GetMessageProbability()
    {
        var value = _random.Next(0, 101);
        var probability = value switch
        {
            > 60 and <= 80 => MessageProbability.Rare,
            > 80 and <= 90 => MessageProbability.Normal,
            > 90 and <= 97 => MessageProbability.Epic,
            > 97 and <= 100 => MessageProbability.Legend,
            _ => MessageProbability.Typical
        };
        return probability;
    }

    public static int GetRandomInt(int min, int max) => _random.Next(min, max);
}