using System.Globalization;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Events;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Admin;

// The clock is the world's and the server's alone. The weather is an area's, and the game of the area's master client
// keeps it changing from the one set, so what it reports of the weather before is refused for a moment.
[Service]
internal sealed partial class WorldCommands(CommandRegistry commands, IEntities entities, Audit audit, SystemClock clock)
{
    private const int HoursInDay = 24;
    private const int MinutesInHour = 60;
    private const float MinutesInDay = HoursInDay * MinutesInHour;
    private const char HourSeparator = ':';
    private const string TimeAction = "admin.time";
    private const string WeatherAction = "admin.weather";
    private const string WeatherSeparator = ", ";

    // The weather at once, not on its way.
    private const float WholeTransition = 1f;

    private readonly StaffSettingRegistry _settings = new(clock);

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Time, AdminText.CmdTime, SetTime);
        commands.AddStaff(StaffPermissions.Weather, AdminText.CmdWeather, SetWeather);
    }

    [EventHandler(Stage.Validate)]
    public void CheckWeather(in WeatherChangeEvent change, EventContext context)
    {
        if (_settings.RefusesWeather(change.Area.AreaId.ToString(), change.WeatherFormId.Value))
            context.Cancel();
    }

    private void SetTime(CommandCall call)
    {
        if (call.Words.Count != 1 || !TryHour(call.Words[0], out var hour))
        {
            call.Refuse(AdminText.TimeUsage);
            return;
        }

        var world = FindWorld();
        world.As<Time>().Advance(TimeSpan.FromMinutes(MinutesUntil(world.TimeOfDayMinutes, hour * MinutesInHour)));
        audit.Log(TimeAction, call.Sender.Ref, details: new TimeDetails(call.Words[0]));
        call.Reply(AdminText.TimeSet(time: call.Words[0]));
    }

    private void SetWeather(CommandCall call)
    {
        if (call.Words.Count != 1 || !WeatherCatalog.Shipped.TryFind(call.Words[0], out var form))
        {
            call.Refuse(AdminText.WeatherUsage(string.Join(WeatherSeparator, WeatherCatalog.Shipped.Names)));
            return;
        }

        if (!TryArea(call, out var area))
            return;

        area.SetWeatherFormId(new FormId(form));
        area.SetTransitionRatio(WholeTransition);
        _settings.HoldWeather(area.AreaId.ToString(), form);
        audit.Log(WeatherAction, call.Sender.Ref, details: new AreaDetails(area.AreaId.ToString(), call.Words[0]));
        call.Reply(AdminText.WeatherSet(area: area.AreaId.ToString(), weather: call.Words[0]));
    }

    // The world's clock only goes forward, so a time of day already gone by is the next day's.
    public static float MinutesUntil(float nowMinutes, float wantedMinutes)
        => wantedMinutes >= nowMinutes ? wantedMinutes - nowMinutes : MinutesInDay - nowMinutes + wantedMinutes;

    // "13" or "13:30", from 0:00 to 23:59.
    public static bool TryHour(string text, out float hour)
    {
        hour = 0;
        var parts = text.Split(HourSeparator);
        if (parts.Length > 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) || hours >= HoursInDay)
            return false;

        var minutes = 0;
        if (parts.Length == 2 && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes) || minutes >= MinutesInHour))
            return false;

        hour = hours + (float)minutes / MinutesInHour;
        return true;
    }

    private World FindWorld()
    {
        foreach (var world in entities.Query<World>())
            return world;

        throw new InvalidOperationException("The server has no world while a player gives it a command.");
    }

    private bool TryArea(CommandCall call, out Area area)
    {
        if (entities.TryLookup(call.Sender.Id, out MainCharacter main) && entities.TryGetScope(main, out area))
            return true;

        area = default;
        call.Refuse(AdminText.NotInWorld(call.Sender.Name));
        return false;
    }

    private sealed record AreaDetails(string Area, string To);

    private sealed record TimeDetails(string To);
}
