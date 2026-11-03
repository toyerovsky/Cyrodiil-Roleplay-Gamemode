using System.Globalization;
using System.Numerics;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Server.Admin;

// The game's own units against Unreal's centimetres, which the server holds: 70 units a metre and Y the other way
// round.
internal static class GameSpace
{
    public const float CentimetresPerUnit = 100f / 70f;

    // The game's exterior grid: a cell is this many of its units on a side, cell 0,0 starting at the origin.
    public const float UnitsPerCell = 4096f;

    private const string OutsideWorld = "Tamriel";
    private const char GridSeparator = ',';

    public static AreaId Outside { get; } = OblivionAreaIds.BuildExterior(OutsideWorld);

    public static Vector3 ToUnreal(float x, float y, float z) => new(x * CentimetresPerUnit, -y * CentimetresPerUnit, z * CentimetresPerUnit);

    public static Vector3 ToGame(Vector3 unreal) => new(unreal.X / CentimetresPerUnit, -unreal.Y / CentimetresPerUnit, unreal.Z / CentimetresPerUnit);

    public static CellId ToExteriorCell(Vector3 unreal)
    {
        var game = ToGame(unreal);
        return OblivionCellIds.Exterior((int)MathF.Floor(game.X / UnitsPerCell), (int)MathF.Floor(game.Y / UnitsPerCell));
    }

    // An interior's one cell is never near another.
    public static bool AreNear(CellId one, CellId other, int cells) =>
        TryGrid(one, out var x, out var y) && TryGrid(other, out var otherX, out var otherY)
        && Math.Abs(x - otherX) <= cells && Math.Abs(y - otherY) <= cells;

    private static bool TryGrid(CellId cell, out int x, out int y)
    {
        x = y = 0;
        var parts = cell.ToManaged().Split(GridSeparator);
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out x)
            && int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out y);
    }
}
