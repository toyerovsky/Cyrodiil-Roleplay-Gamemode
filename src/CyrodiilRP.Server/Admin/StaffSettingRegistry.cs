using CyrodiilRP.Server.Infrastructure;

namespace CyrodiilRP.Server.Admin;

// The weather and the clock the staff last set in each area, each held for HoldFor. The area's master client reports
// what its game shows whenever that differs from the server's, and in the tick a new value reaches it, it reports the
// old one first and only then takes the new. The server would apply that report over the staff's word.
internal sealed class StaffSettingRegistry(IClock clock)
{
    public static readonly TimeSpan HoldFor = TimeSpan.FromSeconds(5);

    // Less than the drift the game's clock makes before the master reports it.
    private const float HourSlack = 0.01f;

    private readonly Dictionary<string, StaffWeather> _weathers = [];
    private readonly Dictionary<string, StaffClock> _clocks = [];

    private sealed record StaffWeather(uint Form, DateTimeOffset Until);

    private sealed record StaffClock(float DaysPassed, float Hour, DateTimeOffset Until);

    public void HoldWeather(string area, uint form) => _weathers[area] = new StaffWeather(form, clock.UtcNow + HoldFor);

    public void HoldClock(string area, float daysPassed, float hour) => _clocks[area] = new StaffClock(daysPassed, hour, clock.UtcNow + HoldFor);

    // A report of another weather than the one just set.
    public bool RefusesWeather(string area, uint reported)
    {
        if (!_weathers.TryGetValue(area, out var held))
            return false;

        if (clock.UtcNow < held.Until)
            return reported != held.Form;

        _weathers.Remove(area);
        return false;
    }

    // A report of a moment before the one just set. A clock set by the staff never goes back (WorldCommands.DaysPassedAt),
    // so an earlier moment is the one the master's game had.
    public bool RefusesClock(string area, float daysPassed, float hour)
    {
        if (!_clocks.TryGetValue(area, out var held))
            return false;

        if (clock.UtcNow < held.Until)
            return daysPassed < held.DaysPassed || (daysPassed == held.DaysPassed && hour < held.Hour - HourSlack);

        _clocks.Remove(area);
        return false;
    }
}
