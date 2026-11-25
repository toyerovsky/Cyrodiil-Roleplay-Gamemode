using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Sanctions;
using CyrodiilRP.Server.Staff;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Events;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Admin;

// /afly: a staff member on duty flies as a camera nobody sees. The server takes the character out of the game's
// physics and hides it, on the player's entity, and the flyer's own game moves it from then (Flight in the client).
[Service]
internal sealed partial class Flights(
    CommandRegistry commands,
    IEntities entities,
    OnlinePlayers online,
    StaffDuty duty,
    Teleports teleports,
    Freezes freezes,
    RpChat chat,
    Audit audit,
    SystemClock clock)
{
    private const string OnAction = "admin.flight_start";
    private const string OffAction = "admin.flight_end";

    private readonly FlightRegistry _flights = new(clock);

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Fly, AdminText.CmdFly, Toggle, needsCharacter: true);
        online.Left += _flights.Forget;
        duty.Changed += OnDutyChanged;
        freezes.Froze += LandUnasked;
    }

    // The server applies every change of health, so a harm refused here is not taken. Healing passes.
    [EventHandler(Stage.Validate)]
    public void CheckHarm(in HealthChangedEvent change, EventContext context)
    {
        if (change.Amount < 0 && change.Victim.TryAs(out MainCharacter struck) && online.TryGet(struck.PlayerId, out var player)
            && _flights.Spares(player))
            context.Cancel();
    }

    private void Toggle(CommandCall call)
    {
        var flyer = call.Sender;
        if (_flights.IsFlying(flyer))
        {
            Land(flyer);
            call.Reply(AdminText.FlyOff);
            return;
        }

        if (freezes.IsFrozen(flyer))
        {
            call.Refuse(AdminText.FlyFrozen);
            return;
        }

        if (!teleports.TryLocate(call, flyer, out var from))
            return;

        if (!entities.TryLookup(flyer.Id, out Player player))
            throw new InvalidOperationException($"{flyer.Ref} is in the world and has no player entity.");

        // /aback returns to where the flight began, as from a teleport.
        teleports.KeepBefore(flyer, from);
        player.IsHidden = true;
        player.IgnoresCollision = true;
        player.IsFrozen = true;
        _flights.TakeOff(flyer);
        audit.Log(OnAction, flyer.Ref);
        call.Reply(AdminText.FlyOn);
    }

    private void OnDutyChanged(OnlinePlayer player)
    {
        if (player.DutySince is null)
            LandUnasked(player);
    }

    private void LandUnasked(OnlinePlayer player)
    {
        if (!_flights.IsFlying(player))
            return;

        Land(player);
        chat.Notify(player, AdminText.FlyOff);
    }

    // A flyer who left is not landed: their entity went with them.
    private void Land(OnlinePlayer flyer)
    {
        _flights.Land(flyer);
        if (entities.TryLookup(flyer.Id, out Player player))
        {
            player.IsHidden = false;
            player.IgnoresCollision = false;

            // A freeze that ended the flight keeps the character still.
            player.IsFrozen = freezes.IsFrozen(flyer);
        }

        audit.Log(OffAction, flyer.Ref);
    }
}
