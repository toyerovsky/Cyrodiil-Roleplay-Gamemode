using System.Numerics;
using CyrodiilRP.Server.Admin;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Server.Traders;

// A trader_spots row as the server keeps it. NpcRefId: the trader standing there now, 0 before one is placed.
internal sealed class TraderSpotRecord(long id, string shop, string area, string cell, Vector3 position, float yaw)
{
    public long Id { get; } = id;

    public string Shop { get; } = shop;

    public string Area { get; } = area;

    public string Cell { get; } = cell;

    public Vector3 Position { get; } = position;

    public float Yaw { get; } = yaw;

    // The cell the trader is made in. Outside, the cell of the spot's position, not the stored one: a spot placed before
    // the game caught up with a far teleport stored the cell the staff member had left.
    public FullCellId Home { get; } = new(new AreaId(area),
        cell == OblivionCellIds.InteriorCellIdName ? OblivionCellIds.Interior : GameSpace.ToExteriorCell(position));

    public uint NpcRefId { get; set; }
}
