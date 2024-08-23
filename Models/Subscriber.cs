using System.ComponentModel.DataAnnotations;

namespace NonStop.SitUpStraight.Bot.Models;

public class Subscriber
{
    [Key]
    public long ChatId { get; set; }
    public int StartHourUtc { get; set; } = 6;
    public int EndHourUtc { get; set;} = 18;
}