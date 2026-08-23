using NonStop.SitUpStraight.Bot.Constants;

namespace NonStop.SitUpStraight.Bot.Helpers;

public static class SettingsHelper
{
    public static string GetSettingsInfo(int startHourUtc, int endHourUtc, int offset, string timezone, int daysPerWeek)
    {
        var days = TimeHelper.DaysMap[daysPerWeek];
        var startHourLocal = TimeHelper.GetHourLocal(startHourUtc, offset);
        var endHourLocal = TimeHelper.GetHourLocal(endHourUtc, offset);
        var info = $"⚙️ <b>{BotCommands.MySettings}:</b>\n" +
                   $"⏰ Время напоминаний: с <b>{startHourLocal}:00</b> по <b>{endHourLocal}:00</b>\n" +
                   $"🌍 Моя таймзона: <b>{timezone}</b>\n" +
                   $"📅 Дни напоминаний: <b>{days}</b>";
        return info;
    }
}