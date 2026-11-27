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

    // An interior has no grid, so its levels hash the interior cell's form id instead of X and Y.
    public (int X, int Y) LevelKey => Kind == ParentCellKind.Interior ? ((int)InteriorFormId(Area), 0) : (X, Y);

    // Interior areas are named L_PersistentDungeon-<cell form id in hex>; anything else yields 0.
    public static uint InteriorFormId(AreaId area)
    {
        var name = area.ToManaged();
        var dash = name.LastIndexOf('-');
        return dash >= 0 && uint.TryParse(name.AsSpan(dash + 1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var formId)
            ? formId : 0;
    }
}
