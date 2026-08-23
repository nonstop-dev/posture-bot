using System.Text.Json;

namespace NonStop.Posture.Bot.Helpers;

public static class SerializationHelper
{
    public readonly static JsonSerializerOptions JsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}