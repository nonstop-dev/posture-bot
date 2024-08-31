namespace NonStop.SitUpStraight.Bot.Models;

public class Timezone
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public int Offset { get; set; }
}