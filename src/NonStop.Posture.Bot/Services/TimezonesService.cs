using System.Text.Json;
using NonStop.Posture.Bot.Helpers;
using NonStop.Posture.Bot.Models;

namespace NonStop.Posture.Bot.Services;

public class TimezonesService : ITimezonesService
{
    private const string TimezonesRelativePath = "Data/timezones.json";
    private readonly Lazy<List<Timezone>> _timezones;

    public TimezonesService()
    {
        _timezones = new Lazy<List<Timezone>>(InitTimezones);
    }

    public List<Timezone> GetTimezones() => _timezones.Value;

    public Timezone GetTimezone(int id) => _timezones.Value.First(t => t.Id == id);

    private static List<Timezone> InitTimezones()
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, TimezonesRelativePath);
        var path = File.Exists(fullPath) ? fullPath : TimezonesRelativePath;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<Timezone>>(json, SerializationHelper.JsonSerializerOptions)
               ?? throw new InvalidOperationException("Не удалось загрузить конфигурацию таймзон");
    }
}