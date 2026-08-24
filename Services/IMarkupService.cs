using Telegram.Bot.Types.ReplyMarkups;

namespace NonStop.Posture.Bot.Services;

public interface IMarkupService
{
    ReplyKeyboardMarkup GetDefaultMarkup();
    InlineKeyboardMarkup GetSettingsInlineMarkup();
    InlineKeyboardMarkup GetStartWizardMarkup();
    InlineKeyboardMarkup GetTimezonesMarkup();
    InlineKeyboardMarkup GetHoursMarkup();
    InlineKeyboardMarkup GetDaysMarkup();
    InlineKeyboardMarkup GetCustomStartHoursMarkup();
    InlineKeyboardMarkup GetCustomEndHoursMarkup(int startHourLocal);
    InlineKeyboardMarkup GetFeedbackRatingMarkup();
    InlineKeyboardMarkup GetFeedbackLikedMarkup();
    InlineKeyboardMarkup GetFeedbackImproveMarkup();
    ReplyKeyboardMarkup GetLocationRequestMarkup();
    InlineKeyboardMarkup GetAdminMenuMarkup();
}