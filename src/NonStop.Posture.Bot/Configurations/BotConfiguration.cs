namespace NonStop.Posture.Bot.Configurations;

public class BotConfiguration
{
    public string BotToken { get; init; } = default!;
    public string? AdminChatId { get; init; }
    public List<long>? AdminChatIds { get; init; }

    public IReadOnlySet<long> GetAdminIds()
    {
        var ids = new HashSet<long>();
        if (AdminChatIds != null)
        {
            foreach (var id in AdminChatIds)
            {
                ids.Add(id);
            }
        }

        if (!string.IsNullOrWhiteSpace(AdminChatId))
        {
            var parts = AdminChatId.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (long.TryParse(part.Trim(), out var parsedId))
                {
                    ids.Add(parsedId);
                }
            }
        }

        return ids;
    }
}