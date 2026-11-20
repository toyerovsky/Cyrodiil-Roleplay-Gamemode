using System.Numerics;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Events;
using OblivionMP.SDK.Serverside;
using OblivionMP.SDK.Serverside.Ai;
using OblivionMP.SDK.Serverside.World;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Traders;

// The cell saves its people, so a trader placed once is there after a restart. Every client in the cell shows it, and
// the cell's master moves it. A banker and a stablemaster are stood and kept the same way, each under an origin of its
// own. None of them takes harm: the server is what applies a change of health, and it refuses theirs.
[Service]
internal sealed partial class TraderNpcs(IEntities entities, WorldRefs refs, ServerRefIds refIds, TraderSpots spots, Proximity proximity, Timers timers)
{
    private const int Level = 1;
    private const string KeepJob = "traders.keep";

    private void Start() => timers.RunEvery(KeepJob, TraderRules.CheckEvery, Keep);

    [EventHandler(Stage.Validate)]
    public void CheckHarm(in HealthChangedEvent change, EventContext context)
    {
        if (change.Amount < 0 && change.Victim.TryAs(out Npc npc) && TraderRules.IsOurs(npc.SpawnerRefId))
            context.Cancel();
    }

    public bool TryFind(TraderSpotRecord spot, out Npc npc) =>
        refs.TryFindNpc(spot.NpcRefId, out npc) && npc.SpawnerRefId == TraderRules.FindOrigin(spot.Kind);

    // The person of ours nearest in front of the player, of any kind.
    public TraderSpotRecord? FindNearestInFront(OnlinePlayer actor, float reach, float coneDegrees, out float distance)
    {
        TraderSpotRecord? nearest = null;
        distance = float.MaxValue;
        foreach (var spot in spots.All)
        {
            if (TryFind(spot, out var npc)
                && proximity.MeasureInFront(actor.Id, new AreaId(spot.Area), npc.Position, reach, coneDegrees) is { } away
                && away < distance)
            {
                nearest = spot;
                distance = away;
            }
        }

        return nearest;
    }

    // The spot of the person of ours standing under the ref id.
    public TraderSpotRecord? FindByNpc(uint npcRefId)
    {
        foreach (var spot in spots.All)
        {
            if (spot.NpcRefId == npcRefId && TryFind(spot, out _))
                return spot;
        }

        return null;
    }

    public void Remove(TraderSpotRecord spot)
    {
        if (TryFind(spot, out var npc))
            entities.Delete(npc);
    }

    // The world's people are walked once a pass, for every spot.
    private void Keep()
    {
        var ours = ListOurs();
        foreach (var spot in spots.All)
            Keep(spot, ours);
    }

    private void Keep(TraderSpotRecord spot, List<Npc> ours)
    {
        var found = TryFindAmong(ours, spot.NpcRefId, out var npc);
        var stray = found && !IsHome(npc, spot);
        if (stray)
            Delete(ours, npc);

        if (!TryFindCell(spot, out var cell) || cell.MasterClient == PlayerId.Invalid)
            return;

        if (!stray && (found || TryAdopt(spot, ours, out npc)))
        {
            if (!IsDead(npc))
                return;

            Delete(ours, npc);
        }

        Place(spot, cell);
    }

    private List<Npc> ListOurs()
    {
        var ours = new List<Npc>();
        foreach (var npc in entities.Query<Npc>())
        {
            if (TraderRules.IsOurs(npc.SpawnerRefId))
                ours.Add(npc);
        }

        return ours;
    }

    private static bool TryFindAmong(List<Npc> ours, uint refId, out Npc found)
    {
        foreach (var npc in ours)
        {
            if (npc.RefId == refId)
            {
                found = npc;
                return true;
            }
        }

        found = default;
        return false;
    }

    private void Delete(List<Npc> ours, Npc npc)
    {
        var refId = npc.RefId;
        ours.RemoveAll(other => other.RefId == refId);
        entities.Delete(npc);
    }

    private bool TryFindCell(TraderSpotRecord spot, out Cell cell) => entities.TryLookup(spot.Home, out cell);

    // The cell's save keeps a trader whose ref id the spot lost (never written, or the server stopped first): the one of
    // ours standing at the spot is taken back instead of a second placed beside it, and any more of ours there go.
    private bool TryAdopt(TraderSpotRecord spot, List<Npc> ours, out Npc found)
    {
        var near = ours
            .Where(npc => npc.SpawnerRefId == TraderRules.FindOrigin(spot.Kind) && !IsHeldByOtherSpot(npc.RefId, spot) && IsHome(npc, spot)
                          && Vector3.Distance(npc.Position, spot.Position) <= TraderRules.AdoptRadius)
            .ToList();
        if (near.Count == 0)
        {
            found = default;
            return false;
        }

        found = near.FirstOrDefault(npc => !IsDead(npc), near[0]);
        foreach (var extra in near)
        {
            if (extra.RefId != found.RefId)
                Delete(ours, extra);
        }

        spots.SetNpc(spot, found.RefId);
        return true;
    }

    // A trader made in a cell far from its spot's (a spot placed before the game caught up with a far teleport) is given
    // a body by no client: the master of that cell has not loaded the place it stands at. One wandering over the edge into
    // the next cell is home.
    private bool IsHome(Npc npc, TraderSpotRecord spot)
    {
        if (!entities.TryGetScope(EntityHandle.Of(npc), out Cell scope))
            return true;

        var at = scope.FullCellId;
        return at == spot.Home
            || (at.AreaId == spot.Home.AreaId && GameSpace.AreNear(at.CellId, spot.Home.CellId, TraderRules.HomeCells));
    }

    private bool IsHeldByOtherSpot(uint refId, TraderSpotRecord spot) =>
        spots.All.Any(other => other != spot && other.NpcRefId == refId);

    // The health the game reports is 0 until the master first sends it, which is not death.
    private static bool IsDead(Npc npc) => npc.HpBase > 0 && npc.Hp <= 0;

    private void Place(TraderSpotRecord spot, Cell cell)
    {
        var npc = entities.Create<Npc>(cell, cell.MasterClient);
        npc.RefId = refIds.Next();
        npc.SpawnerRefId = TraderRules.FindOrigin(spot.Kind);
        npc.BaseId = new FormId(TraderRules.BaseNpc);
        npc.Position = spot.Position;
        npc.Rotation = spot.Yaw;
        npc.Level = Level;

        var outfit = EntityHandle.Of(npc).As<Equipment>();
        foreach (var piece in TraderRules.FindOutfit(spot.Kind))
            outfit.AddItems(WornClothes.CreateEntry(piece));

        AiIntents.Enqueue(npc, new AiIntentEntry
        {
            PackageType = AiPackageType.ExploreWander,
            LocationPosition = spot.Position,
            Radius = TraderRules.WanderRadius,
        });
        spots.SetNpc(spot, npc.RefId);
    }
}
