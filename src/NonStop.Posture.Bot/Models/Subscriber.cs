using System.ComponentModel.DataAnnotations;
using NonStop.Posture.Bot.Helpers;

namespace NonStop.Posture.Bot.Models;

public class Subscriber
{
    [Key]
    public long ChatId { get; set; }
    public int StartHourUtc { get; set; } = 9;
    public int EndHourUtc { get; set;} = 21;
    public int Offset { get; set; } = 3;
    public int DaysPerWeek { get; set; } = 7;
    public bool Configured { get; set; }
    public int TotalMessagesSent { get; set; }
    public int LegendaryCount { get; set; }
}