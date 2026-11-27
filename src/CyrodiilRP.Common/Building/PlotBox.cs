using System.Numerics;

namespace CyrodiilRP.Common.Building;

// The space of a plot: its tiles along the world's axes, from GroundZ up to Height centimetres above it. A piece may
// sink below the ground, so only its top is held to the height.
public readonly record struct PlotBox(int FirstTileX, int FirstTileY, int Width, int Depth, float GroundZ, float Height)
{
    // A piece's box may touch the plot's edge this much over, so one placed flush is not refused for a rounding.
    private const float EdgeSlack = 1f;

    public float MinX => BuildingGrid.EdgeOf(FirstTileX);

    public float MinY => BuildingGrid.EdgeOf(FirstTileY);

    public float MaxX => BuildingGrid.EdgeOf(FirstTileX + Width);

    public float MaxY => BuildingGrid.EdgeOf(FirstTileY + Depth);

    public float Top => GroundZ + Height;

    public Vector3 Center => new((MinX + MaxX) / 2f, (MinY + MaxY) / 2f, GroundZ + Height / 2f);

    public Vector3 HalfSize => new((MaxX - MinX) / 2f, (MaxY - MinY) / 2f, Height / 2f);

    public bool Contains(float x, float y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

    public bool Overlaps(PlotBox other) =>
        FirstTileX < other.FirstTileX + other.Width && other.FirstTileX < FirstTileX + Width
        && FirstTileY < other.FirstTileY + other.Depth && other.FirstTileY < FirstTileY + Depth;

    // A mesh's box (min and max along its own axes, at its scale) with its origin at position, turned by yaw degrees.
    public PlotFit Judge(Vector3 min, Vector3 max, Vector3 position, float yaw)
    {
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw * MathF.PI / 180f);
        foreach (var corner in Corners(min, max))
        {
            var at = position + Vector3.Transform(corner, turn);
            if (at.X < MinX - EdgeSlack || at.X > MaxX + EdgeSlack || at.Y < MinY - EdgeSlack || at.Y > MaxY + EdgeSlack)
                return PlotFit.Outside;
        }

        return position.Z + max.Z > Top + EdgeSlack ? PlotFit.TooHigh : PlotFit.Fits;
    }

    // The rectangle of tiles between two points, both corners included.
    public static PlotBox Between(Vector3 first, Vector3 second, float groundZ, float height)
    {
        var fromX = BuildingGrid.TileOf(MathF.Min(first.X, second.X));
        var fromY = BuildingGrid.TileOf(MathF.Min(first.Y, second.Y));
        var toX = BuildingGrid.TileOf(MathF.Max(first.X, second.X));
        var toY = BuildingGrid.TileOf(MathF.Max(first.Y, second.Y));
        return new PlotBox(fromX, fromY, toX - fromX + 1, toY - fromY + 1, groundZ, height);
    }

    private static Vector3[] Corners(Vector3 min, Vector3 max) =>
    [
        new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z),
        new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z), new(min.X, max.Y, max.Z), new(max.X, max.Y, max.Z),
    ];
}
