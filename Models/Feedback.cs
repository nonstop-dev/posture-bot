using System.ComponentModel.DataAnnotations;

namespace NonStop.SitUpStraight.Bot.Models;

public class Feedback
{
    [Key]
    public int Id { get; set; }
    public long ChatId { get; set; }
    public int? Rating { get; set; }
    public string? LikedOption { get; set; }
    public string? ImproveOption { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
