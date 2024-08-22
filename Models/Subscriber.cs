using System.ComponentModel.DataAnnotations;

namespace NonStop.SitUpStraight.Bot.Models;

public class Subscriber
{
    [Key]
    public long ChatId { get; set; }
}