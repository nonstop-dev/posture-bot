using NonStop.SitUpStraight.Bot.Constants;
using Telegram.Bot.Types.ReplyMarkups;

namespace NonStop.SitUpStraight.Bot.Services;

public class MarkupService(ITimezonesService timezonesService) : IMarkupService
{
    public ReplyKeyboardMarkup GetDefaultMarkup() => new(
            new List<KeyboardButton[]>()
            {
                new KeyboardButton[]
                {
                    new(BotCommands.SelectTimezone),
                    new(BotCommands.SelectDays),
                    new(BotCommands.SelectHours)
                },
                new KeyboardButton[]
                {
                    new(BotCommands.MySettings)
                }
            })
    { ResizeKeyboard = true };

    public InlineKeyboardMarkup GetTimezonesMarkup()
    {
        var timezones = timezonesService.GetTimezones();
        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var t in timezones)
        {
            var data = $"{MarkupCommands.Timezone}--{t.Id}";
            var button = new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData(t.Title, data)
            };
            buttons.Add(button);
        }
        var markup = new InlineKeyboardMarkup(buttons);

        return markup;
    }

    public InlineKeyboardMarkup GetHoursMarkup()
    {
        var command = MarkupCommands.Hours;
        var buttons = new List<InlineKeyboardButton[]>
        {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("9:00 - 21:00", $"{command}--9--21"),
                InlineKeyboardButton.WithCallbackData("10:00 - 20:00", $"{command}--10--20")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("9:00 - 20:00", $"{command}--9--20"),
                InlineKeyboardButton.WithCallbackData("10:00 - 21:00", $"{command}--10--21")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("Настроить", $"{command}--custom--0")
            }
        };

        var markup = new InlineKeyboardMarkup(buttons);

        return markup;
    }

    public InlineKeyboardMarkup GetDaysMarkup()
    {
        var command = MarkupCommands.Days;
        var buttons = new List<InlineKeyboardButton[]>
        {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("ПН-ПТ", $"{command}--5"),
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("ПН-СБ", $"{command}--6")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("ПН-ВС", $"{command}--7")
            }
        };

        var markup = new InlineKeyboardMarkup(buttons);

        return markup;
    }
}