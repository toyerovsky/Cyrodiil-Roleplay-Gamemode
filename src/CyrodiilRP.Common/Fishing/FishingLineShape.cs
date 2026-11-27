using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// The line from the rod's tip to the float as every game near the angler draws it, in straight pieces: taut it is one
// straight line, slack it hangs in a curve.
public static class FishingLineShape
{
    public const int Pieces = 6;

    // The point a share of the way from the tip to the float. Sag: how far the middle hangs under the straight line,
    // as a share of the line's length.
    public static Vector3 PointAt(Vector3 tip, Vector3 end, float sag, float share)
    {
        var along = Math.Clamp(share, 0f, 1f);
        var straight = Vector3.Lerp(tip, end, along);
        return straight with { Z = straight.Z - Vector3.Distance(tip, end) * sag * 4f * along * (1f - along) };
    }
}
