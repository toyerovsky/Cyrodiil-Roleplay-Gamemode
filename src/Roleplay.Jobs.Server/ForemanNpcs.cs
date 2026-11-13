using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Serverside;
using OblivionMP.SDK.Serverside.Ai;
using OblivionMP.SDK.Serverside.Spawners;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace Roleplay.Jobs.Server;

/// <summary>
/// Stands the foremen at their places and keeps them there, the way the gamemode keeps its traders: the cell saves its
/// people, so a foreman made once is taken back after a restart (adopted by origin and place), one killed or gone is
/// made again once its cell has a master, and their health is kept full. Each role has an origin of its own (the
/// SpawnerRefId), which the clients label them by.
/// </summary>
/// <remarks>Server thread only.</remarks>
[Service]
public sealed partial class ForemanNpcs(IEntities entities, ServerRefIds refIds, DocksStore store, JobsConfig config, ILogger logger)
{
    // The roles' origins count up from here: clear of the gamemode's traders (0xFFFFFFFC, 0xFFFFFFFD)
    private const uint FirstOrigin = 0xFFFFFF00;
    private const int Level = 1;
    private const double KeepEverySeconds = 2;
    private const double RefillEverySeconds = 0.25;
    // A foreman of ours this near their place after a restart is theirs
    private const float AdoptRadius = 300f;
    private const float WanderRadius = 150f;
    private const string InteriorCell = "Interior";
    // The game's units against Unreal's centimetres, and its exterior cell size in units
    private const float CentimetresPerUnit = 100f / 70f;
    private const float UnitsPerCell = 4096f;

    private readonly Dictionary<string, Npc> _standing = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _nextKeep;
    private double _nextRefill;

    private DocksConfig Settings => config.Docks;

    public static int OriginOf(int roleIndex) => unchecked((int)(FirstOrigin + (uint)roleIndex));

    /// <summary>Where the foreman's role is in the config, or -1 when the config no longer has it.</summary>
    public int RoleIndexOf(Foreman foreman) => Settings.Foremen.FindIndex(role => role.Name == foreman.Role);

    /// <summary>The person standing as the foreman now, if any.</summary>
    public bool TryFind(Foreman foreman, out Npc npc) => _standing.TryGetValue(foreman.Id, out npc) && EntityHandle.Of(npc).IsAlive();

    private void Update()
    {
        var now = _clock.Elapsed.TotalSeconds;
        if (now >= _nextKeep)
        {
            _nextKeep = now + KeepEverySeconds;
            Keep();
        }
        if (now >= _nextRefill)
        {
            _nextRefill = now + RefillEverySeconds;
            Refill();
        }
    }

    /// <summary>A new foreman of the role at the spot, turned to yaw, in the player's world. Null when the player is in no world.</summary>
    public Foreman? Place(PlayerId player, ForemanRole role, Vector3 at, float yaw)
    {
        if (!entities.TryLookup(player, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
            return null;

        var parent = main.As<ParentCell>();
        var cell = parent.ToFullCellId(area.AreaId);
        var cellName = parent.Kind == ParentCellKind.Exterior ? cell.CellId.ToManaged() : InteriorCell;
        var foreman = new Foreman("jobs/foreman/" + Guid.NewGuid().ToString("N"), role.Name, cell.AreaId.ToManaged(), cellName,
            at.X, at.Y, at.Z, yaw, 0);
        store.Add(foreman);
        Keep();
        return foreman;
    }

    public void Remove(Foreman foreman)
    {
        if (TryFind(foreman, out var npc))
            entities.Delete(npc);
        _standing.Remove(foreman.Id);
        store.Remove(foreman.Id);
    }

    private void Keep()
    {
        var ours = ListOurs();
        DeleteOrphans(ours);
        foreach (var foreman in store.Foremen.ToList())
        {
            var role = RoleIndexOf(foreman);
            if (role < 0)
            {
                _standing.Remove(foreman.Id);
                continue; // its role left the config: logged at start
            }
            if (Keep(foreman, OriginOf(role), ours) is { } npc)
                _standing[foreman.Id] = npc;
            else
                _standing.Remove(foreman.Id);
        }
    }

    private Npc? Keep(Foreman foreman, int origin, List<Npc> ours)
    {
        if (TryFindAmong(ours, foreman.NpcRefId, out var npc) && npc.SpawnerRefId == unchecked((uint)origin))
        {
            if (!IsDead(npc))
                return npc;
            Delete(ours, npc);
        }

        if (!entities.TryLookup(HomeOf(foreman), out Cell cell) || cell.MasterClient == PlayerId.Invalid)
            return null;
        if (TryAdopt(foreman, origin, ours, out npc))
            return npc;
        return Make(foreman, origin, cell);
    }

    private List<Npc> ListOurs()
    {
        var ours = new List<Npc>();
        var last = FirstOrigin + (uint)Settings.Foremen.Count;
        foreach (var npc in entities.Query<Npc>())
        {
            if (npc.SpawnerRefId >= FirstOrigin && npc.SpawnerRefId < last)
                ours.Add(npc);
        }
        return ours;
    }

    // One of ours that no foreman in the store holds and that stands near none of its role's places was left by a
    // foreman removed or cleared from the save: it goes
    private void DeleteOrphans(List<Npc> ours)
    {
        foreach (var npc in ours.ToList())
        {
            var refId = npc.RefId;
            var origin = npc.SpawnerRefId;
            var wanted = store.Foremen.Any(foreman => foreman.NpcRefId == refId
                || (RoleIndexOf(foreman) >= 0 && unchecked((uint)OriginOf(RoleIndexOf(foreman))) == origin
                    && Vector3.Distance(npc.Position, foreman.Position) <= AdoptRadius));
            if (wanted)
                continue;
            Delete(ours, npc);
            logger.LogInformation("Jobs: {Npc} stood as a foreman no longer in the save, removed.", refId);
        }
    }

    private static bool TryFindAmong(List<Npc> ours, uint refId, out Npc found)
    {
        foreach (var npc in ours)
        {
            if (refId != 0 && npc.RefId == refId)
            {
                found = npc;
                return true;
            }
        }
        found = default;
        return false;
    }

    // The cell's save keeps a foreman whose ref id the store lost: the one of ours at the place is taken back instead
    // of a second made beside it, and any more of ours there go
    private bool TryAdopt(Foreman foreman, int origin, List<Npc> ours, out Npc found)
    {
        var near = ours
            .Where(npc => npc.SpawnerRefId == unchecked((uint)origin) && !IsHeldByOther(npc.RefId, foreman)
                          && Vector3.Distance(npc.Position, foreman.Position) <= AdoptRadius)
            .ToList();
        if (near.Count == 0)
        {
            found = default;
            return false;
        }

        var kept = near.FirstOrDefault(npc => !IsDead(npc), near[0]);
        foreach (var extra in near)
        {
            if (extra.RefId != kept.RefId)
                Delete(ours, extra);
        }
        if (IsDead(kept))
        {
            Delete(ours, kept);
            found = default;
            return false;
        }
        store.Replace(foreman with { NpcRefId = kept.RefId });
        logger.LogInformation("Jobs: took back {Npc} as {Foreman}.", kept.RefId, foreman.Id);
        found = kept;
        return true;
    }

    private bool IsHeldByOther(uint refId, Foreman foreman) => store.Foremen.Any(other => other.Id != foreman.Id && other.NpcRefId == refId);

    private void Delete(List<Npc> ours, Npc npc)
    {
        var refId = npc.RefId;
        ours.RemoveAll(other => other.RefId == refId);
        entities.Delete(npc);
    }

    private Npc Make(Foreman foreman, int origin, Cell cell)
    {
        var npc = entities.Create<Npc>(cell, cell.MasterClient);
        npc.RefId = refIds.Next();
        npc.SpawnerRefId = unchecked((uint)origin);
        npc.BaseId = new FormId(JobItemResolver.ParseFormId(Settings.BaseNpc));
        npc.Position = foreman.Position;
        npc.Rotation = foreman.Yaw;
        npc.Level = Level;
        AiIntents.Enqueue(npc, new AiIntentEntry
        {
            PackageType = AiPackageType.ExploreWander,
            LocationPosition = foreman.Position,
            Radius = WanderRadius,
        });
        store.Replace(foreman with { NpcRefId = npc.RefId });
        logger.LogInformation("Jobs: stood {Role} {Npc} at {At}.", foreman.Role, npc.RefId, foreman.Position);
        return npc;
    }

    // A blow larger than the whole bar still kills, and Keep stands a new one
    private void Refill()
    {
        foreach (var npc in _standing.Values)
        {
            if (EntityHandle.Of(npc).IsAlive() && !IsDead(npc) && npc.Hp < npc.HpBase)
                npc.Hp = npc.HpBase;
        }
    }

    // The health the game reports is 0 until the master first sends it, which is not death
    private static bool IsDead(Npc npc) => npc.HpBase > 0 && npc.Hp <= 0;

    // Outside, the cell of the place itself, worked out from the position
    private static FullCellId HomeOf(Foreman foreman)
    {
        var area = new AreaId(foreman.Area);
        if (foreman.Cell == InteriorCell)
            return new FullCellId(area, OblivionCellIds.Interior);
        var gameX = foreman.X / CentimetresPerUnit;
        var gameY = -foreman.Y / CentimetresPerUnit;
        return new FullCellId(area, OblivionCellIds.Exterior((int)MathF.Floor(gameX / UnitsPerCell), (int)MathF.Floor(gameY / UnitsPerCell)));
    }
}
