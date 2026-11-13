using System.Diagnostics;
using System.Numerics;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Players;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;
using ReadyM.SDK.Server.Events;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The docks job. An inlet keeps one crate lying in it, cargo, a new one a while after the last was taken. A cargo crate
/// put down in an outlet is delivered: the dock foreman owes whoever put it there a wage (<see cref="WageStore"/>), it
/// can no longer be picked up, and it is cleared away a while later. With setup allowed, players place and remove the
/// deposits and the foremen (<see cref="ForemanNpcs"/>) on the numpad.
/// </summary>
/// <remarks>Server thread only.</remarks>
[Service]
public sealed partial class DockService(
    IEntities entities,
    IServerEvents serverEvents,
    IPlayers players,
    CarryService carrying,
    ForemanNpcs foremen,
    DocksRpcHandlers rpc,
    DocksStore store,
    WageStore wages,
    JobsConfig config,
    ILogger logger)
{
    private const double CheckEverySeconds = 1;
    private const string ZoneIdPrefix = "jobs/dock/";
    private const char SlotSeparator = '#';
    // What a client may measure a crate or a board as, in centimeters
    private const float SmallestMeasure = 0.1f;
    private const float LargestMeasure = 1000f;

    private readonly HashSet<PlayerId> _ready = [];
    // When each inlet slot (the inlet's id, '#' and its number) was first seen empty, and when each crate in an outlet
    // was first seen there
    private readonly Dictionary<string, double> _emptySince = [];
    private readonly Dictionary<string, double> _deliveredAt = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _nextCheck;
    private CarryItem? _crate;

    private DocksConfig Settings => config.Docks;
    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        _crate = CarryItem.AllOf(config.Carrying).FirstOrDefault(kind => kind.Name == Settings.CrateKind)
                 ?? throw new InvalidOperationException($"Jobs config: Docks.CrateKind '{Settings.CrateKind}' is no Carrying kind.");
        foreach (var foreman in store.Foremen.Where(foreman => foremen.RoleIndexOf(foreman) < 0))
            logger.LogWarning("Jobs: foreman {Id} is a {Role}, which Docks.Foremen does not list: left in the save file, not stood.", foreman.Id, foreman.Role);

        carrying.PutDownBy += OnPutDown;
        carrying.AddPickUpRule(RefuseDelivered);
        carrying.AddPickUpRule(RefuseCovered);
        rpc.CrateMeasured += OnCrateMeasured;
        rpc.BoardMeasured += OnBoardMeasured;
        rpc.Ready += OnReady;
        rpc.ForemanPlaceRequested += OnPlaceForeman;
        rpc.ZonePlaceRequested += OnPlaceZone;
        rpc.RemoveRequested += OnRemove;
        serverEvents.OnPlayerDisconnected += OnDisconnected;

        logger.LogInformation("Jobs: docks running. {Inlets} inlets, {Outlets} outlets, {Foremen} foremen; {Wage} gold a crate; setup {Setup}.",
            store.Zones.Count(zone => zone.Kind == DockZoneKind.Inlet), store.Zones.Count(zone => zone.Kind == DockZoneKind.Outlet),
            store.Foremen.Count, Settings.Wage, Settings.AllowSetup ? "on" : "off");
    }

    private void Stop()
    {
        carrying.PutDownBy -= OnPutDown;
        rpc.Ready -= OnReady;
        rpc.ForemanPlaceRequested -= OnPlaceForeman;
        rpc.ZonePlaceRequested -= OnPlaceZone;
        rpc.RemoveRequested -= OnRemove;
        rpc.CrateMeasured -= OnCrateMeasured;
        rpc.BoardMeasured -= OnBoardMeasured;
        serverEvents.OnPlayerDisconnected -= OnDisconnected;
    }

    private void Update()
    {
        if (Now < _nextCheck)
            return;
        _nextCheck = Now + CheckEverySeconds;
        Restock();
        ClearDelivered();
    }

    private void Restock()
    {
        var crate = store.CrateSize;
        // A deposit's floor is where a crate's origin rests on the ground, so a crate on the pallet is the pallet's height up
        var lift = PalletNow()?.Height ?? 0f;
        var lying = carrying.Lying.Select(carryable => carryable.Position).ToList();
        foreach (var inlet in store.Zones.Where(zone => zone.Kind == DockZoneKind.Inlet))
        {
            // Bottom layer first, so a crate is only laid on crates already there
            var slots = InletSlots.Of(inlet, Settings.InletRows, Settings.InletLayers, crate, Settings.CrateGap, Settings.CrateSpacing,
                Settings.ZoneSize, lift);
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                var key = inlet.Id + SlotSeparator + index;
                if (lying.Any(position => InletSlots.IsTaken(slot, position, crate, Settings.CrateSpacing)))
                {
                    _emptySince.Remove(key);
                    continue;
                }
                var supported = InletSlots.Supports(slot, slots)
                    .All(below => lying.Any(position => InletSlots.IsTaken(below, position, crate, Settings.CrateSpacing)));
                if (!supported)
                {
                    _emptySince.Remove(key);
                    continue;
                }
                if (!_emptySince.TryGetValue(key, out var since))
                {
                    _emptySince[key] = Now;
                    continue;
                }
                if (Now - since < Settings.RestockSeconds)
                    continue;
                _emptySince.Remove(key);
                carrying.LayCargo(Settings.CrateKind, slot.Position, inlet.Yaw);
                lying.Add(slot.Position);
            }
        }
    }

    // Cargo found in an outlet without a delivery seen (put there before a restart) is cleared as well
    private void ClearDelivered()
    {
        var outlets = store.Zones.Where(zone => zone.Kind == DockZoneKind.Outlet).ToList();
        foreach (var carryable in carrying.Lying.ToList())
        {
            if (!carryable.Cargo || !outlets.Any(outlet => outlet.Holds(carryable.Position, Settings.ZoneSize)))
                continue;
            if (!_deliveredAt.TryGetValue(carryable.Id, out var at))
            {
                _deliveredAt[carryable.Id] = Now;
                continue;
            }
            if (Now - at < Settings.ClearSeconds)
                continue;
            _deliveredAt.Remove(carryable.Id);
            carrying.Clear(carryable.Id);
        }
    }

    private void OnPutDown(PlayerId player, Carryable carryable)
    {
        if (!carryable.Cargo || !InOutlet(carryable.Position))
            return;

        _deliveredAt[carryable.Id] = Now;
        if (players.GetReadyMId(player) is not { } account)
        {
            logger.LogWarning("Jobs: {Player} delivered {Id} but has no ReadyM account, so is owed nothing.", player, carryable.Id);
            return;
        }
        var unpaid = wages.AddCrate(account);
        Say(player, $"Crate delivered. The dock foreman owes you {unpaid * Settings.Wage} gold.", refusal: false);
        logger.LogInformation("Jobs: {Player} delivered {Id}; {Unpaid} crates unpaid.", player, carryable.Id, unpaid);
    }

    // A crate in an inlet's pile comes off the top: one with another on it stays
    private string? RefuseCovered(Carryable carryable)
    {
        if (!carryable.Cargo || !store.Zones.Any(zone => zone.Kind == DockZoneKind.Inlet && zone.Holds(carryable.Position, Settings.ZoneSize)))
            return null;
        var crate = store.CrateSize;
        return carrying.Lying.Any(other => other.Id != carryable.Id && InletSlots.IsCovered(carryable.Position, other.Position, crate, Settings.CrateSpacing))
            ? "Take the crate on top first."
            : null;
    }

    // Measured by a player allowed to set up the docks: the pile is packed by it from the next restock on
    private void OnCrateMeasured(PlayerId player, Vector3 size, float bottom)
    {
        if (!Settings.AllowSetup)
            return;
        if (!IsMeasurable(size) || MathF.Abs(bottom) > size.Z)
        {
            logger.LogWarning("Jobs: {Player} measured the crate as {Size}, its bottom {Bottom} from its origin, which cannot be. Ignored.",
                player, size, bottom);
            return;
        }
        if (Vector3.Distance(store.CrateSize, size) < 0.5f && MathF.Abs(store.CrateBottom - bottom) < 0.5f)
            return;
        store.SetCrate(size, bottom);
        var cleared = LayPilesAgain();
        logger.LogInformation("Jobs: {Player} measured the crate as {Size} cm, its bottom {Bottom:0.0} cm from its origin; inlets pack their piles by it ({Cleared} crates laid again).",
            player, size, bottom, cleared);
    }

    // The board the pallets are built of, measured the same way: the piles stand on the pallets from then on
    private void OnBoardMeasured(PlayerId player, Vector3 min, Vector3 max)
    {
        if (!Settings.AllowSetup)
            return;
        if (!IsMeasurable(max - min))
        {
            logger.LogWarning("Jobs: {Player} measured the pallet board as {Min} to {Max}, which cannot be. Ignored.", player, min, max);
            return;
        }
        if (Vector3.Distance(store.BoardMin, min) < 0.5f && Vector3.Distance(store.BoardMax, max) < 0.5f)
            return;
        store.SetBoard(min, max);
        var cleared = LayPilesAgain();
        logger.LogInformation("Jobs: {Player} measured the pallet board as {Min} to {Max} cm; pallets stand {Height:0.0} cm high ({Cleared} crates laid again).",
            player, min, max, PalletNow()?.Height ?? 0f, cleared);
    }

    private static bool IsMeasurable(Vector3 size)
        => size.X is >= SmallestMeasure and <= LargestMeasure && size.Y is >= SmallestMeasure and <= LargestMeasure
           && size.Z is >= SmallestMeasure and <= LargestMeasure;

    // The piles laid before were by another size or on another pallet: they go, are laid again by this one, and every
    // player is told how the pallets look now
    private int LayPilesAgain()
    {
        var inlets = store.Zones.Where(zone => zone.Kind == DockZoneKind.Inlet).ToList();
        var cleared = carrying.Lying.Where(carryable => carryable.Cargo && inlets.Any(inlet => inlet.Holds(carryable.Position, Settings.ZoneSize)))
            .Select(carryable => carryable.Id).ToList();
        foreach (var id in cleared)
            carrying.Clear(id);
        _emptySince.Clear();
        foreach (var ready in _ready)
            SendRules(ready);
        return cleared.Count;
    }

    private Vector2 PalletFootprint()
    {
        var pile = InletSlots.Footprint(Settings.InletRows, store.CrateSize, Settings.CrateGap, Settings.CrateSpacing, Settings.ZoneSize);
        return pile + new Vector2(2f * Settings.PalletMargin);
    }

    // Null without a board configured or measured
    private Pallet? PalletNow()
        => Settings.PalletBoard.Length == 0
            ? null
            : PalletLayout.Of(PalletFootprint(), store.BoardMin, store.BoardMax, Settings.PalletWoodShare, Settings.PalletRunnerInset);

    private string? RefuseDelivered(Carryable carryable)
        => carryable.Cargo && InOutlet(carryable.Position) ? "That crate is delivered." : null;

    private bool InOutlet(Vector3 point)
        => store.Zones.Any(zone => zone.Kind == DockZoneKind.Outlet && zone.Holds(point, Settings.ZoneSize));

    private void OnReady(PlayerId player)
    {
        _ready.Add(player);
        SendRules(player);
        for (var index = 0; index < Settings.Foremen.Count; index++)
            rpc.SendForemanLabel(player, ForemanNpcs.OriginOf(index), Settings.Foremen[index].Name);
        foreach (var zone in store.Zones)
            SendZone(player, zone);
    }

    private void SendRules(PlayerId player)
    {
        var crate = _crate ?? throw new InvalidOperationException("Jobs: the docks were asked for before they started.");
        var pallet = PalletFootprint();
        rpc.SendDocksRules(player, Settings.AllowSetup, Settings.ZoneSize, crate.Asset, crate.Scale,
            string.Join('|', Settings.Foremen.Select(role => role.Name)), Settings.PalletBoard, pallet.X, pallet.Y,
            Settings.PalletWoodShare, Settings.PalletRunnerInset, store.CrateBottom);
    }

    private void SendZone(PlayerId player, DockZone zone)
        => rpc.SendZoneAvailable(player, zone.Id, (byte)zone.Kind, zone.X, zone.Y, zone.Z, zone.Yaw);

    private void OnPlaceForeman(PlayerId player, int roleIndex, Vector3 at, float yaw)
    {
        if (!MaySetUp(player) || !entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;
        if (!float.IsFinite(at.X) || !float.IsFinite(at.Y) || !float.IsFinite(at.Z) || !float.IsFinite(yaw)
            || Vector3.Distance(character.Position, at) > Settings.Reach + Settings.ZoneSize)
        {
            logger.LogWarning("Jobs: {Player} at {Standing} asked for a foreman at {At}, out of reach.", player, character.Position, at);
            Say(player, "That is too far to place a foreman.", refusal: true);
            return;
        }
        if (roleIndex < 0 || roleIndex >= Settings.Foremen.Count)
        {
            Say(player, "There is no foreman on that key.", refusal: true);
            return;
        }
        var role = Settings.Foremen[roleIndex];
        if (foremen.Place(player, role, at, yaw) is not { } foreman)
            return;
        Say(player, $"Placed the {role.Name}.", refusal: false);
        logger.LogInformation("Jobs: {Player} placed {Role} {Id} at {At}.", player, role.Name, foreman.Id, foreman.Position);
    }

    private void OnPlaceZone(PlayerId player, DockZoneKind kind, Vector3 floor, float yaw)
    {
        if (!MaySetUp(player) || !entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;
        if (Vector3.Distance(character.Position, floor) > Settings.Reach + Settings.ZoneSize)
        {
            logger.LogWarning("Jobs: {Player} at {Standing} asked for a deposit at {At}, out of reach.", player, character.Position, floor);
            Say(player, "That is too far to place a deposit.", refusal: true);
            return;
        }

        var zone = new DockZone(ZoneIdPrefix + Guid.NewGuid().ToString("N"), kind, floor.X, floor.Y, floor.Z, yaw);
        store.Add(zone);
        foreach (var ready in _ready)
            SendZone(ready, zone);
        Say(player, kind switch
        {
            DockZoneKind.Inlet => "Placed a crate inlet.",
            DockZoneKind.Outlet => "Placed a crate outlet.",
        }, refusal: false);
        logger.LogInformation("Jobs: {Player} placed {Kind} {Id} at {At}.", player, kind, zone.Id, floor);
    }

    // The nearest of the deposits and foremen within reach goes
    private void OnRemove(PlayerId player)
    {
        if (!MaySetUp(player) || !entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;

        var standing = character.Position;
        var zone = store.Zones.MinBy(zone => ChopGeometry.HorizontalDistance(zone.Floor, standing));
        var foreman = store.Foremen.MinBy(foreman => ChopGeometry.HorizontalDistance(PositionOf(foreman), standing));
        var zoneAway = zone is null ? float.MaxValue : ChopGeometry.HorizontalDistance(zone.Floor, standing);
        var foremanAway = foreman is null ? float.MaxValue : ChopGeometry.HorizontalDistance(PositionOf(foreman), standing);
        if (Math.Min(zoneAway, foremanAway) > Settings.Reach)
        {
            Say(player, "There is no foreman or deposit near enough to remove.", refusal: true);
            return;
        }

        if (zone is not null && zoneAway <= foremanAway)
        {
            store.Remove(zone.Id);
            foreach (var key in _emptySince.Keys.Where(key => key.StartsWith(zone.Id + SlotSeparator, StringComparison.Ordinal)).ToList())
                _emptySince.Remove(key);
            foreach (var ready in _ready)
                rpc.SendZoneRemoved(ready, zone.Id);
            Say(player, "Removed the deposit.", refusal: false);
            logger.LogInformation("Jobs: {Player} removed {Id}.", player, zone.Id);
            return;
        }
        if (foreman is not null)
        {
            foremen.Remove(foreman);
            Say(player, $"Removed the {foreman.Role}.", refusal: false);
            logger.LogInformation("Jobs: {Player} removed {Id}.", player, foreman.Id);
        }
    }

    private Vector3 PositionOf(Foreman foreman) => foremen.TryFind(foreman, out var npc) ? npc.Position : foreman.Position;

    private bool MaySetUp(PlayerId player)
    {
        if (Settings.AllowSetup)
            return true;
        logger.LogWarning("Jobs: {Player} tried to set up the docks, which this server does not allow.", player);
        Say(player, "This server does not let you place foremen or deposits.", refusal: true);
        return false;
    }

    private void Say(PlayerId player, string message, bool refusal) => rpc.SendDocksNote(player, message, refusal);

    private void OnDisconnected(Player scope, Guid _, DisconnectReason reason) => _ready.Remove(scope.PlayerId);
}
