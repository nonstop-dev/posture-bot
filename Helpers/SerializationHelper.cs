using System.Text.Json;

namespace NonStop.SitUpStraight.Bot.Helpers;

public static class SerializationHelper
{
    public readonly static JsonSerializerOptions JsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}