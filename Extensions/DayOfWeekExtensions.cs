namespace NonStop.SitUpStraight.Bot.Extensions;

public static class DayOfWeekExtensions
{
    public static int ToDayNumber(this DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => 1,
        DayOfWeek.Tuesday => 2,
        DayOfWeek.Wednesday => 3,
        DayOfWeek.Thursday => 4,
        DayOfWeek.Friday => 5,
        DayOfWeek.Saturday => 6,
        DayOfWeek.Sunday => 7,
        _ => throw new NotImplementedException()
    };
}