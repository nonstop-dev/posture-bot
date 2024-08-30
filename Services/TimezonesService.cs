using System.Text.Json;
using NonStop.SitUpStraight.Bot.Models;
using NonStop.SitUpStraight.Bot.Services;

public class TimezonesService : ITimezonesService
{
    private readonly Lazy<List<Timezone>> _timezones;

    public TimezonesService()
    {
        _timezones = new Lazy<List<Timezone>>(InitTimezones);
    }

    public List<Timezone> GetTimezones() => _timezones.Value;

    private List<Timezone> InitTimezones()
    {
        // todo: maybe move file name separately
        using var reader = new StreamReader("Data/timezones.json");
        var json = reader.ReadToEnd();
        // todo: serialization settings should be placed separately
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var timezones = JsonSerializer.Deserialize<List<Timezone>>(json, options);
        return timezones!;
    }
}