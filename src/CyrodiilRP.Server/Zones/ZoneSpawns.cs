using CyrodiilRP.Server.Infrastructure;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Serverside;
using OblivionMP.SDK.Serverside.Archetypes;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Zones;

[Service]
internal sealed partial class ZoneSpawns(IEntities entities, ZoneResolver resolver, SessionScaling scaling, Watchdog watchdog)
{
    public const int BeforeRespawn = 99;
    public const int AfterRespawn = 101;

    [UpdateOrder(BeforeRespawn)]
    private void Update() => watchdog.Run("zones.offsets", SetOffsets);

    private void SetOffsets()
    {
        var sessionLevel = scaling.CurrentLevel();
        var levels = new Dictionary<ZoneCell, ZoneResolution?>();
        foreach (var spawner in entities.Query<Spawner>())
        {
            // Spawned lists too: the SDK can reset and refill a list in one tick, after this pass has run.
            var cell = CellOf(spawner);
            if (!levels.TryGetValue(cell, out var resolution))
            {
                resolution = resolver.Resolve(cell);
                levels.Add(cell, resolution);
            }

            var offset = resolution is null ? 0 : ZoneLevels.Offset(resolution.Level, sessionLevel);
            if (spawner.LevelOffset != offset)
                spawner.LevelOffset = offset;
        }
    }

    public void CorrectLevels() => watchdog.Run("zones.npc_levels", CorrectSpawnLevels);

    private void CorrectSpawnLevels()
    {
        var levels = new Dictionary<SpawnOrigin, ushort>();
        foreach (var spawner in entities.Query<Spawner>())
        {
            var cell = CellOf(spawner);
            if (spawner.State == SpawnState.Spawned && resolver.Find(cell) is not null)
                levels[new SpawnOrigin(cell, spawner.RefId)] = (ushort)Math.Max(ZoneLevels.Minimum, (int)spawner.Level);
        }

        foreach (var npc in entities.Query<Npc>())
        {
            if (resolver.TryLocate(npc, out var cell)
                && levels.TryGetValue(new SpawnOrigin(cell, npc.SpawnerRefId), out var level) && npc.Level != level)
                npc.Level = level;
        }
    }

    public static ZoneCell CellOf(Spawner spawner) =>
        new(spawner.Area, spawner.ParentCellKind, spawner.ParentCellX, spawner.ParentCellY);

    private readonly record struct SpawnOrigin(ZoneCell Cell, uint RefId);
}
