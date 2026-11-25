using System.Numerics;

namespace CyrodiilRP.Server.Fishing;

// A named water of the map, its outline ready to be looked up.
internal sealed record MappedWater(string Id, string Name, WaterKind Kind, float SurfaceZ, IReadOnlyList<Vector2> Outline)
{
    // Even-odd: a point inside crosses the outline an odd number of times going one way.
    public bool Holds(Vector2 point)
    {
        var inside = false;
        for (int i = 0, j = Outline.Count - 1; i < Outline.Count; j = i++)
        {
            var a = Outline[i];
            var b = Outline[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }

        return inside;
    }
}
