namespace NonStop.Posture.Bot.Helpers;

public static class TimeHelper
{
    public static readonly Dictionary<int, string> DaysMap = new() {
        { 5, "ПН-ПТ" },
        { 6, "ПН-СБ" },
        { 7, "ПН-ВС" }
    };

    public static int NormalizeHour(int hour) => ((hour % 24) + 24) % 24;

    public static int GetHourUtc(int hourLocal, int offset) => NormalizeHour(hourLocal - offset);

    public static int GetHourLocal(int hourUtc, int offset) => NormalizeHour(hourUtc + offset);

    public static int RoundHourIfNeed(int hour) => NormalizeHour(hour);
}