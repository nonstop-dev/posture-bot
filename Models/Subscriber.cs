using System.ComponentModel.DataAnnotations;

namespace NonStop.SitUpStraight.Bot.Models;

public class Subscriber
{
    [Key]
    public long ChatId { get; set; }
    public int StartHour { get; set; } = 9;
    public int EndHour { get; set;} = 21;
    public int Offset { get; set; } = 3;
}