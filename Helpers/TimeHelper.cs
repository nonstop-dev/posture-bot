namespace NonStop.SitUpStraight.Bot.Helpers;

public static class TimeHelper
{
    private const int FirstHour = 1;
    private const int LastHour = 24;

    public static int GetStartHourUtc(int userStartHour, int offset)
    {
        var hour = userStartHour - offset;
        if (hour < FirstHour)
        {
            hour = LastHour + hour;
        }

        return hour;
    }

    public static int GetEndHourUtc(int userEndHour, int offset)
    {
        var hour = userEndHour - offset;
        if (hour >= LastHour)
        {
            hour = hour - LastHour;
        }

        return hour;
    }
}