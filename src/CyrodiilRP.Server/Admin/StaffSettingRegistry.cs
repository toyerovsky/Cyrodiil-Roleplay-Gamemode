using CyrodiilRP.Server.Infrastructure;

namespace CyrodiilRP.Server.Admin;

// The weather the staff last set in each area, held for HoldFor. The area's master client reports what its game shows
// whenever that differs from the server's, and in the tick a new value reaches it, it reports the old one first and
// only then takes the new. The server would apply that report over the staff's word.
internal sealed class StaffSettingRegistry(IClock clock)
{
    public static readonly TimeSpan HoldFor = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, StaffWeather> _weathers = [];

    private sealed record StaffWeather(uint Form, DateTimeOffset Until);

    public void HoldWeather(string area, uint form) => _weathers[area] = new StaffWeather(form, clock.UtcNow + HoldFor);

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
}
