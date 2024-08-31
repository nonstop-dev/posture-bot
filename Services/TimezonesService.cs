using System.Text.Json;
using NonStop.SitUpStraight.Bot.Helpers;
using NonStop.SitUpStraight.Bot.Models;

namespace NonStop.SitUpStraight.Bot.Services;

public class TimezonesService : ITimezonesService
{
    private readonly Lazy<List<Timezone>> _timezones;

    public TimezonesService()
    {
        _timezones = new Lazy<List<Timezone>>(InitTimezones);
    }

    public List<Timezone> GetTimezones() => _timezones.Value;

    public Timezone GetTimezone(int id) => _timezones.Value.First(t => t.Id == id);

    private List<Timezone> InitTimezones()
    {
        // todo: maybe move file name separately
        using var reader = new StreamReader("Data/timezones.json");
        var json = reader.ReadToEnd();
        var timezones = JsonSerializer.Deserialize<List<Timezone>>(json, SerializationHelper.JsonSerializerOptions);
        return timezones!;
    }
}