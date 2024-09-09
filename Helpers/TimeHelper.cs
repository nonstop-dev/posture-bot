namespace NonStop.SitUpStraight.Bot.Helpers;

public static class TimeHelper
{
    private const int FirstHour = 1;
    private const int LastHour = 24;

    public static readonly Dictionary<int, string> DaysMap = new() {
        { 5, "ПН-ПТ" },
        { 6, "ПН-СБ" },
        { 7, "ПН-ВС" }
    };

    public static int GetHourUtc(int hourLocal, int offset) => RoundHourIfNeed(hourLocal - offset);

    public static int GetHourLocal(int hourUtc, int offset) => RoundHourIfNeed(hourUtc + offset);

    public static int RoundHourIfNeed(int hour)
    {
        if (hour < FirstHour)
        {
            hour += LastHour;
        }
        if (hour >= LastHour)
        {
            hour -= LastHour;
        }

        return hour;
    }
}