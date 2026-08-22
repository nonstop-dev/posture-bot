using NonStop.SitUpStraight.Bot.Constants;
using Telegram.Bot.Types.ReplyMarkups;

namespace NonStop.SitUpStraight.Bot.Services;

public class MarkupService(ITimezonesService timezonesService) : IMarkupService
{
    public ReplyKeyboardMarkup GetDefaultMarkup() => new(
        new[]
        {
            new KeyboardButton[]
            {
                new(BotCommands.StatsMenu),
                new(BotCommands.SettingsMenu)
            },
            new KeyboardButton[]
            {
                new(BotCommands.FeedbackMenu),
                new(BotCommands.HelpMenu)
            }
        })
    { ResizeKeyboard = true };

    public InlineKeyboardMarkup GetSettingsInlineMarkup() => new(
        new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🌍 Выбрать таймзону", "set_tz"),
                InlineKeyboardButton.WithCallbackData("📅 Выбрать дни", "set_days")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏰ Выбрать время", "set_hours"),
                InlineKeyboardButton.WithCallbackData("📋 Мои настройки", "set_info")
            }
        });

    public InlineKeyboardMarkup GetStartWizardMarkup() => new(
        new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🚀 Погнали!", MarkupCommands.StartWizard)
            }
        });

    public InlineKeyboardMarkup GetTimezonesMarkup()
    {
        var timezones = timezonesService.GetTimezones();
        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var t in timezones)
        {
            var data = $"{MarkupCommands.Timezone}--{t.Id}";
            buttons.Add([InlineKeyboardButton.WithCallbackData(t.Title, data)]);
        }

        buttons.Add([InlineKeyboardButton.WithCallbackData("📍 Определить автоматически", $"{MarkupCommands.Timezone}--auto")]);

        return new InlineKeyboardMarkup(buttons);
    }

    public InlineKeyboardMarkup GetHoursMarkup()
    {
        var command = MarkupCommands.Hours;
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("9:00 - 21:00", $"{command}--9--21"),
                    InlineKeyboardButton.WithCallbackData("10:00 - 20:00", $"{command}--10--20")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("9:00 - 20:00", $"{command}--9--20"),
                    InlineKeyboardButton.WithCallbackData("10:00 - 21:00", $"{command}--10--21")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⚙️ Настроить своё время", $"{command}--custom--0")
                }
            });
    }

    public InlineKeyboardMarkup GetCustomStartHoursMarkup()
    {
        var command = MarkupCommands.CustomHourStart;
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("6:00", $"{command}--6"),
                    InlineKeyboardButton.WithCallbackData("7:00", $"{command}--7"),
                    InlineKeyboardButton.WithCallbackData("8:00", $"{command}--8")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("9:00", $"{command}--9"),
                    InlineKeyboardButton.WithCallbackData("10:00", $"{command}--10"),
                    InlineKeyboardButton.WithCallbackData("11:00", $"{command}--11")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("12:00", $"{command}--12"),
                    InlineKeyboardButton.WithCallbackData("13:00", $"{command}--13"),
                    InlineKeyboardButton.WithCallbackData("14:00", $"{command}--14")
                }
            });
    }

    public InlineKeyboardMarkup GetCustomEndHoursMarkup(int startHourLocal)
    {
        var buttons = new List<InlineKeyboardButton[]>();
        var row = new List<InlineKeyboardButton>();

        var minEnd = Math.Max(startHourLocal + 1, 15);
        for (int h = minEnd; h <= 23; h++)
        {
            row.Add(InlineKeyboardButton.WithCallbackData($"{h}:00", $"{MarkupCommands.CustomHourEnd}--{startHourLocal}--{h}"));
            if (row.Count == 3)
            {
                buttons.Add(row.ToArray());
                row.Clear();
            }
        }

        if (row.Count > 0)
        {
            buttons.Add(row.ToArray());
        }

        return new InlineKeyboardMarkup(buttons);
    }

    public InlineKeyboardMarkup GetDaysMarkup()
    {
        var command = MarkupCommands.Days;
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("ПН – ПТ (Будни)", $"{command}--5")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("ПН – СБ (6 дней)", $"{command}--6")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("ПН – ВС (Каждый день)", $"{command}--7")
                }
            });
    }

    public InlineKeyboardMarkup GetFeedbackRatingMarkup() => new(
        new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("1 ⭐️", $"{MarkupCommands.FeedbackRating}--1"),
                InlineKeyboardButton.WithCallbackData("2 ⭐️", $"{MarkupCommands.FeedbackRating}--2"),
                InlineKeyboardButton.WithCallbackData("3 ⭐️", $"{MarkupCommands.FeedbackRating}--3"),
                InlineKeyboardButton.WithCallbackData("4 ⭐️", $"{MarkupCommands.FeedbackRating}--4"),
                InlineKeyboardButton.WithCallbackData("5 ⭐️", $"{MarkupCommands.FeedbackRating}--5")
            }
        });

    public InlineKeyboardMarkup GetFeedbackLikedMarkup() => new(
        new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏰ Настройка времени", $"{MarkupCommands.FeedbackLiked}--time")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🎲 Разнообразные напоминалки", $"{MarkupCommands.FeedbackLiked}--variety")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔇 Нет лишнего спама", $"{MarkupCommands.FeedbackLiked}--nospam")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✍️ Написать свой вариант", $"{MarkupCommands.FeedbackLiked}--custom")
            }
        });

    public InlineKeyboardMarkup GetFeedbackImproveMarkup() => new(
        new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🌍 Больше таймзон", $"{MarkupCommands.FeedbackImprove}--tz")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("💬 Больше упражнений и фраз", $"{MarkupCommands.FeedbackImprove}--messages")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📝 Предложить свою фразу", $"{MarkupCommands.FeedbackImprove}--suggest")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✍️ Написать свой вариант", $"{MarkupCommands.FeedbackImprove}--custom")
            }
        });

    public ReplyKeyboardMarkup GetLocationRequestMarkup() => new(
        new[]
        {
            new KeyboardButton[]
            {
                KeyboardButton.WithRequestLocation("📍 Отправить геопозицию")
            },
            new KeyboardButton[]
            {
                new("Отмена")
            }
        })
    {
        ResizeKeyboard = true,
        OneTimeKeyboard = true
    };
}