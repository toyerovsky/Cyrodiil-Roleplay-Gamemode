using CyrodiilRP.Server.Party;
using CyrodiilRP.Server.Players;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Zones;

[Service]
internal sealed partial class ZoneResolver(RpConfig config, OnlinePlayers online, Parties parties, IEntities entities)
{
    private readonly ZoneMap _map = new(config.Zones);

    public ZoneDefinition? Find(ZoneCell cell) => _map.Find(cell);

    // All consumers read the cell's level here until the SDK supports a cell level component.
    public ZoneResolution? Resolve(ZoneCell cell)
    {
        if (Find(cell) is not { } zone)
            return null;

        var lowest = zone.Mode switch
        {
            ZoneMode.Fixed => ZoneLevels.Minimum,
            ZoneMode.ScalesAbove or ZoneMode.PartyLowest => ZoneLevels.Lowest(
                online.All.Where(player => TryLocate(player.Id, out var at) && at == cell), parties.MembersOf, ReadLevel),
        };
        return new ZoneResolution(zone, ZoneLevels.ForCell(zone, cell, lowest));
    }

    public bool TryLocate(PlayerId player, out ZoneCell cell)
    {
        if (entities.TryLookup(player, out MainCharacter main) && entities.TryGetScope(main, out Area area))
        {
            cell = new ZoneCell(area.AreaId, main.ParentCellKind, main.ParentCellX, main.ParentCellY);
            return true;
        }
        cell = default;
        return false;
    }

    public bool TryLocate(Npc npc, out ZoneCell cell)
    {
        // Npc.Cell is a handoff target, so the current cell comes from its entity scope.
        if (entities.TryGetScope(EntityHandle.Of(npc), out Cell scope))
        {
            cell = ZoneCell.From(scope.FullCellId);
            return true;
        }
        cell = default;
        return false;
    }

    private int? ReadLevel(OnlinePlayer player) => player.IsLive && entities.TryLookup(player.Id, out MainCharacter main) ? main.Level : null;
}
