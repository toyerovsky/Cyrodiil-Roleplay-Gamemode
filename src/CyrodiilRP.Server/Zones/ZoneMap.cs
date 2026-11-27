using CyrodiilRP.Server.Admin;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Server.Zones;

internal sealed class ZoneMap(ZoneSettings settings)
{
    public const int Coordinates = 2;
    public const int FewestPoints = 3;

    private readonly IReadOnlyList<MappedZone> _zones = settings.Entries
        .Select(zone => new MappedZone(zone, zone.Areas.Select(OblivionAreaIds.BuildExterior).ToHashSet(),
            zone.Interiors.Select(cell => OblivionAreaIds.BuildInterior(ParseFormId(cell))).ToHashSet())).ToList();

    public ZoneDefinition? Find(ZoneCell cell)
    {
        if (!settings.Enabled)
            return null;

        if (cell.Kind == ParentCellKind.Interior)
            return _zones.FirstOrDefault(zone => zone.Interiors.Contains(cell.Area))?.Definition;

        if (cell.Kind != ParentCellKind.Exterior)
            return null;

        return _zones.FirstOrDefault(zone => zone.Areas.Contains(cell.Area)
            || (cell.Area == GameSpace.Outside && zone.Definition.Shapes.Any(shape => Contains(shape, cell.X, cell.Y))))?.Definition;
    }

    // Interior cells are listed by their form id in hex, as the catalog and the game write them.
    public static bool TryParseFormId(string text, out uint formId) =>
        uint.TryParse(text, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out formId)
        && formId != 0;

    private static uint ParseFormId(string text) => TryParseFormId(text, out var formId)
        ? formId : throw new FormatException($"Interior cell '{text}' is not a form id in hex.");

    public static bool Contains(int[][] polygon, int x, int y)
    {
        var inside = false;
        for (var index = 0; index < polygon.Length; index++)
        {
            var start = polygon[index];
            var end = polygon[(index + 1) % polygon.Length];
            var cross = ((double)x - start[0]) * ((double)end[1] - start[1])
                - ((double)y - start[1]) * ((double)end[0] - start[0]);
            if (cross == 0 && x >= Math.Min(start[0], end[0]) && x <= Math.Max(start[0], end[0])
                && y >= Math.Min(start[1], end[1]) && y <= Math.Max(start[1], end[1]))
                return true;

            if ((start[1] > y) != (end[1] > y)
                && x < ((double)end[0] - start[0]) * ((double)y - start[1]) / ((double)end[1] - start[1]) + start[0])
                inside = !inside;
        }
        return inside;
    }

    private sealed record MappedZone(ZoneDefinition Definition, HashSet<AreaId> Areas, HashSet<AreaId> Interiors);
}
