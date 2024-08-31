using NonStop.SitUpStraight.Bot.Models;

namespace NonStop.SitUpStraight.Bot.Services;

public interface ITimezonesService
{
    List<Timezone> GetTimezones();
    Timezone GetTimezone(int id);
}