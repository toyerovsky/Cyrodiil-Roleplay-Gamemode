using System.Numerics;

namespace CyrodiilRP.Common.Building;

// Where a plot's sign stands: at the middle of its front, a little outside it, turned away from the plot. The client
// finds the ground under that spot, the server checks it stays at the plot's edge.
public static class PlotSigns
{
    // How far outside the front's edge the sign stands, in centimetres.
    public const float Outside = 60f;

    public static Vector2 SpotOn(PlotBox box, PlotEdge edge)
    {
        var middleX = (box.MinX + box.MaxX) / 2f;
        var middleY = (box.MinY + box.MaxY) / 2f;
        return edge switch
        {
            PlotEdge.LowY => new Vector2(middleX, box.MinY - Outside),
            PlotEdge.HighX => new Vector2(box.MaxX + Outside, middleY),
            PlotEdge.HighY => new Vector2(middleX, box.MaxY + Outside),
            PlotEdge.LowX => new Vector2(box.MinX - Outside, middleY),
        };
    }

    // The way the sign faces, away from the plot, in degrees.
    public static float YawOf(PlotEdge edge) => edge switch
    {
        PlotEdge.LowY => -90f,
        PlotEdge.HighX => 0f,
        PlotEdge.HighY => 90f,
        PlotEdge.LowX => 180f,
    };

    public static PlotEdge Next(PlotEdge edge) => edge switch
    {
        PlotEdge.LowY => PlotEdge.HighX,
        PlotEdge.HighX => PlotEdge.HighY,
        PlotEdge.HighY => PlotEdge.LowX,
        PlotEdge.LowX => PlotEdge.LowY,
    };

    // The side that faces a point most: the one a viewer there sees the front of.
    public static PlotEdge Facing(PlotBox box, Vector2 from)
    {
        var away = from - new Vector2((box.MinX + box.MaxX) / 2f, (box.MinY + box.MaxY) / 2f);
        if (MathF.Abs(away.X) >= MathF.Abs(away.Y))
            return away.X >= 0f ? PlotEdge.HighX : PlotEdge.LowX;

        return away.Y >= 0f ? PlotEdge.HighY : PlotEdge.LowY;
    }

    // The side a sign stands at: the one nearest to it.
    public static PlotEdge EdgeOf(PlotBox box, Vector2 sign) => Enum.GetValues<PlotEdge>()
        .OrderBy(edge => Vector2.Distance(SpotOn(box, edge), sign))
        .First();
}
