using System.Numerics;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Admin;

// /afly: a staff member on duty flies through walls. It is made of teleports (FlightRules), so it runs only for
// those who fly and ends with the duty.
[Service]
internal sealed partial class Flights(
    CommandRegistry commands,
    IEntities entities,
    OnlinePlayers online,
    StaffDuty duty,
    Teleports teleports,
    RpChat chat,
    Audit audit,
    Timers timers)
{
    private const string OnAction = "admin.flight_start";
    private const string OffAction = "admin.flight_end";
    private const string MoveJob = "fly.move";

    private readonly Dictionary<OnlinePlayer, Hold> _flying = [];

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Fly, AdminText.CmdFly, Toggle, needsCharacter: true);
        online.Left += player => _flying.Remove(player);
        duty.Changed += OnDutyChanged;
        teleports.Moved += OnMoved;
        timers.RunEvery(MoveJob, FlightRules.MoveEvery, Move);
    }

    private void Toggle(CommandCall call)
    {
        var flyer = call.Sender;
        if (_flying.Remove(flyer))
        {
            audit.Log(OffAction, flyer.Ref);
            call.Reply(AdminText.FlyOff);
            return;
        }

        if (!entities.TryLookup(flyer.Id, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
        {
            call.Refuse(AdminText.NotInWorld(flyer.Name));
            return;
        }

        _flying[flyer] = new Hold(area.AreaId, main.Position.Z);
        audit.Log(OnAction, flyer.Ref);
        call.Reply(AdminText.FlyOn);
    }

    private void OnDutyChanged(OnlinePlayer player)
    {
        if (player.DutySince is null && _flying.Remove(player))
        {
            audit.Log(OffAction, player.Ref);
            chat.Notify(player, AdminText.FlyOff);
        }
    }

    // A teleport, /astep up and down among them, sets the height a flyer holds from then on.
    private void OnMoved(OnlinePlayer player, AreaId area, Vector3 position)
    {
        if (_flying.ContainsKey(player))
            _flying[player] = new Hold(area, position.Z);
    }

    // A flyer out of the world for a moment (a door loading) is passed over. One come out in another area holds the
    // height they stand at there: the old one belongs to the place they left.
    private void Move()
    {
        if (_flying.Count == 0)
            return;

        foreach (var (flyer, hold) in _flying.ToList())
        {
            if (!entities.TryLookup(flyer.Id, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
                continue;

            var here = main.Position;
            if (area.AreaId != hold.Area)
            {
                _flying[flyer] = new Hold(area.AreaId, here.Z);
                continue;
            }

            var next = FlightRules.FindNext(here, ReadPush(main), hold.Height);
            if (Vector3.Distance(here, next) > FlightRules.Slack)
                main.Position = next;
        }
    }

    // The game hands the push of this frame on to "last" as it uses it up, so either may hold it.
    private static Vector3 ReadPush(in MainCharacter main)
    {
        var movement = main.As<AnimationMovement>();
        var push = movement.ControlInputVector;
        return push == Vector3.Zero ? movement.LastControlInputVector : push;
    }

    // Height: the game's centimetres, in the area it was taken in.
    private sealed record Hold(AreaId Area, float Height);
}
