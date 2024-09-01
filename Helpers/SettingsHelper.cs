using NonStop.SitUpStraight.Bot.Constants;

namespace NonStop.SitUpStraight.Bot.Helpers;

public static class SettingsHelper
{
    public static string GetSettingsInfo(int startHourUtc, int endHourUtc, int offset, string timezone, int daysPerWeek)
    {
        var days = TimeHelper.DaysMap[daysPerWeek];
        var startHourLocal = TimeHelper.GetHourLocal(startHourUtc, offset);
        var endHourLocal = TimeHelper.GetHourLocal(endHourUtc, offset);
        var info = $@"{BotCommands.MySettings}:
Время напоминаний с {startHourLocal}:00 по {endHourLocal}:00
Моя таймзона: {timezone}
Дни напоминаний: {days}";
        return info;
    }
}