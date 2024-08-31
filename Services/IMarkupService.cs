using Telegram.Bot.Types.ReplyMarkups;

namespace NonStop.SitUpStraight.Bot.Services;

public interface IMarkupService
{
    ReplyKeyboardMarkup GetDefaultMarkup();
    InlineKeyboardMarkup GetTimezonesMarkup();
    InlineKeyboardMarkup GetHoursMarkup();
}