using System.Globalization;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Server.Zones;

internal readonly record struct ZoneCell(AreaId Area, ParentCellKind Kind, int X, int Y)
{
    public static ZoneCell From(FullCellId cell)
    {
        if (cell.CellId == OblivionCellIds.Interior)
            return new ZoneCell(cell.AreaId, ParentCellKind.Interior, 0, 0);

        var coordinates = cell.CellId.ToManaged().Split(',');
        if (coordinates.Length != ZoneMap.Coordinates)
            throw new FormatException($"Cell '{cell}' has no exterior grid coordinates.");

        return new ZoneCell(cell.AreaId, ParentCellKind.Exterior,
            int.Parse(coordinates[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
            int.Parse(coordinates[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
    }
}
