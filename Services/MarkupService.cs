using NonStop.Posture.Bot.Constants;
using Telegram.Bot.Types.ReplyMarkups;

namespace NonStop.Posture.Bot.Services;

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
                    InlineKeyboardButton.WithCallbackData("09:00 - 21:00 (Весь день)", $"{command}--9--21"),
                    InlineKeyboardButton.WithCallbackData("10:00 - 19:00 (Рабочее)", $"{command}--10--19")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("10:00 - 22:00 (Поздний день)", $"{command}--10--22"),
                    InlineKeyboardButton.WithCallbackData("00:00 - 04:00 (Ночной режим)", $"{command}--0--4")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⚙️ Настроить своё время (Любые часы)", $"{command}--custom--0")
                }
            });
    }

    public InlineKeyboardMarkup GetCustomStartHoursMarkup()
    {
        var command = MarkupCommands.CustomHourStart;
        var rows = new List<InlineKeyboardButton[]>();
        for (int r = 0; r < 6; r++)
        {
            var row = new InlineKeyboardButton[4];
            for (int c = 0; c < 4; c++)
            {
                int hour = r * 4 + c;
                row[c] = InlineKeyboardButton.WithCallbackData($"{hour:D2}:00", $"{command}--{hour}");
            }
            rows.Add(row);
        }
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup GetCustomEndHoursMarkup(int startHourLocal)
    {
        var command = MarkupCommands.CustomHourEnd;
        var rows = new List<InlineKeyboardButton[]>();
        for (int r = 0; r < 6; r++)
        {
            var row = new InlineKeyboardButton[4];
            for (int c = 0; c < 4; c++)
            {
                int hour = r * 4 + c;
                var label = hour == startHourLocal ? $"{hour:D2}:00 (старт)" : $"{hour:D2}:00";
                row[c] = InlineKeyboardButton.WithCallbackData(label, $"{command}--{startHourLocal}--{hour}");
            }
            rows.Add(row);
        }
        return new InlineKeyboardMarkup(rows);
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

    public InlineKeyboardMarkup GetAdminMenuMarkup() => new(
        new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Статистика", $"{MarkupCommands.Admin}--stats"),
                InlineKeyboardButton.WithCallbackData("⭐️ Отзывы", $"{MarkupCommands.Admin}--feedback")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🌍 Пояса пользователей", $"{MarkupCommands.Admin}--tz"),
                InlineKeyboardButton.WithCallbackData("📢 Рассылка", $"{MarkupCommands.Admin}--broadcast")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Обновить панель", $"{MarkupCommands.Admin}--stats")
            }
        });

    public InlineKeyboardMarkup GetBroadcastConfirmMarkup() => new(
        new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🚀 Подтвердить и отправить", $"{MarkupCommands.Admin}--bcast_confirm"),
                InlineKeyboardButton.WithCallbackData("❌ Отменить", $"{MarkupCommands.Admin}--bcast_cancel")
            }
        });
}