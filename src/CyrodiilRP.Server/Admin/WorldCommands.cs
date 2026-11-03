using System.Globalization;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Admin;

// The game of the area's master client keeps its clock going from the hour set.
[Service]
internal sealed partial class WorldCommands(CommandRegistry commands, IEntities entities, Audit audit)
{
    private const int HoursInDay = 24;
    private const int MinutesInHour = 60;
    private const char HourSeparator = ':';
    private const string TimeAction = "admin.time";
    private const string WeatherAction = "admin.weather";
    private const string WeatherSeparator = ", ";

    // The weather at once, not on its way.
    private const float WholeTransition = 1f;

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Time, AdminText.CmdTime, SetTime);
        commands.AddStaff(StaffPermissions.Weather, AdminText.CmdWeather, SetWeather);
    }

    private void SetTime(CommandCall call)
    {
        if (call.Words.Count != 1 || !TryHour(call.Words[0], out var hour))
        {
            call.Refuse(AdminText.TimeUsage);
            return;
        }

        if (!TryArea(call, out var area))
            return;

        area.SetGameHour(hour);
        audit.Log(TimeAction, call.Sender.Ref, details: new AreaDetails(area.AreaId.ToString(), call.Words[0]));
        call.Reply(AdminText.TimeSet(area: area.AreaId.ToString(), time: call.Words[0]));
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
        audit.Log(WeatherAction, call.Sender.Ref, details: new AreaDetails(area.AreaId.ToString(), call.Words[0]));
        call.Reply(AdminText.WeatherSet(area: area.AreaId.ToString(), weather: call.Words[0]));
    }

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

    private bool TryArea(CommandCall call, out Area area)
    {
        if (entities.TryLookup(call.Sender.Id, out MainCharacter main) && entities.TryGetScope(main, out area))
            return true;

        area = default;
        call.Refuse(AdminText.NotInWorld(call.Sender.Name));
        return false;
    }

    private sealed record AreaDetails(string Area, string To);
}
