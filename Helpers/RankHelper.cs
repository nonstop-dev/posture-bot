namespace NonStop.SitUpStraight.Bot.Helpers;

public static class RankHelper
{
    public static string GetRank(int totalMessagesSent) => totalMessagesSent switch
    {
        < 50 => "🏔 Гора",
        < 100 => "🪵 Палка",
        < 250 => "🐈 Выпрямившийся котик",
        < 500 => "🏛 Античная колонна",
        < 1000 => "🦾 Стальной хребет",
        < 2000 => "⚡ Титан осанки",
        _ => "🌅 Великая Равнина"
    };
}
