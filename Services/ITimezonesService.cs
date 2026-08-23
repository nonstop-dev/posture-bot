using NonStop.Posture.Bot.Models;

namespace NonStop.Posture.Bot.Services;

public interface ITimezonesService
{
    List<Timezone> GetTimezones();
    Timezone GetTimezone(int id);
}