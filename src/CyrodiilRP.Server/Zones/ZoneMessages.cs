using System.Globalization;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using OblivionMP.SDK.Serverside;
using OblivionMP.SDK.Serverside.Archetypes;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Zones;

[Service]
internal sealed partial class ZoneMessages(
    OnlinePlayers online, ZoneResolver resolver, Localizer texts, RpcHandlers rpc,
    Timers timers, CommandRegistry commands, IEntities entities, SessionScaling scaling)
{
    private const string PlayerCommand = "zone";
    private const string WatchJob = "zones.enter";
    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);
    private readonly Dictionary<OnlinePlayer, string> _shown = [];

    private void Start()
    {
        commands.Add(new CommandSpec { Name = PlayerCommand, Usage = ZonesText.CmdZone, Category = CommandCategory.General, NeedsCharacter = true }, Show);
        commands.AddStaff(StaffPermissions.Zone, ZonesText.CmdStaff, Inspect, needsCharacter: true);
        online.Left += player => _shown.Remove(player);
        timers.RunEvery(WatchJob, WatchEvery, Watch);
    }

    private void Watch()
    {
        foreach (var player in online.All.Where(player => player.IsPlaying))
        {
            // A house or a dungeon has no zone; the last one shown stays, so walking back out does not show it again.
            var zone = resolver.TryLocate(player.Id, out var cell) ? resolver.Find(cell) : null;
            if (zone is null)
                continue;
            if (_shown.TryGetValue(player, out var shown) && shown == zone.Id)
                continue;

            _shown[player] = zone.Id;
            rpc.SendCenterMessage(player.Id, texts.Get(player.Locale, Describe(zone, player.Locale)));
        }
    }

    private static Phrase Describe(ZoneDefinition zone, string locale) => zone.Mode switch
    {
        ZoneMode.Fixed or ZoneMode.Gradient => ZonesText.Range(zone.Named(locale), zone.MinLevel, zone.MaxLevel),
        ZoneMode.ScalesAbove => ZonesText.Above(zone.Named(locale), zone.MinLevel),
        ZoneMode.PartyLowest => ZonesText.Party(zone.Named(locale)),
    };

    private void Show(CommandCall call)
    {
        if (!resolver.TryLocate(call.Sender.Id, out var cell))
        {
            call.Refuse(ZonesText.NotInWorld);
            return;
        }
        call.Reply(resolver.Find(cell) is { } zone ? Describe(zone, call.Sender.Locale) : ZonesText.None);
    }

    private void Inspect(CommandCall call)
    {
        if (!resolver.TryLocate(call.Sender.Id, out var cell))
        {
            call.Refuse(ZonesText.NotInWorld);
            return;
        }
        var resolution = resolver.Resolve(cell);
        var offsets = new List<string>();
        foreach (var spawner in entities.Query<Spawner>())
        {
            if (ZoneSpawns.CellOf(spawner) == cell)
                offsets.Add(string.Create(CultureInfo.InvariantCulture, $"{spawner.RefId:X8}: {spawner.LevelOffset}"));
        }
        call.Reply(ZonesText.Diagnostics(cell.Area.ToString(), cell.Kind.ToString(), cell.X, cell.Y,
            resolution is null ? texts.Get(call.Sender.Locale, ZonesText.None) : resolution.Zone.Named(call.Sender.Locale),
            resolution?.Level.ToString(CultureInfo.InvariantCulture) ?? "-", scaling.CurrentLevel(),
            offsets.Count == 0 ? texts.Get(call.Sender.Locale, ZonesText.NoSpawners) : string.Join(", ", offsets)));
    }
}
