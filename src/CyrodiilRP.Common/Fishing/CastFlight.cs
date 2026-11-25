using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// The cast's throw: how far its power sends the float, where on the water it may come down, and its arc through the air.
// Centimetres and seconds.
public static class CastFlight
{
    public const float ShortestSeconds = 0.6f;
    public const float LongestSeconds = 1.1f;

    // A throw this far or farther flies the longest time.
    public const float LongThrow = 2200f;

    // How high the arc rises over the straight way, at the least and for each centimetre thrown.
    public const float RiseLeast = 80f;
    public const float RisePerCm = 0.15f;

    // From nothing to full power the throw goes from the nearest a cast lands to the rod's reach.
    public static float Distance(float power, float reach)
    {
        var farthest = Math.Max(reach, FishingReach.Nearest);
        return FishingReach.Nearest + Math.Clamp(power, 0f, 1f) * (farthest - FishingReach.Nearest);
    }

    // The points a throw may land on along a yaw: from its distance back towards the angler, then on past it as far as
    // the rod reaches, for a throw too short to meet the water.
    public static IEnumerable<Vector3> LandingPoints(Vector3 stand, float yaw, float distance, float reach)
    {
        var way = FishingShot.Forward(yaw);
        for (var along = distance; along >= FishingReach.Nearest; along -= FishingShot.CastStep)
            yield return stand + way * along;

        for (var along = distance + FishingShot.CastStep; along <= reach; along += FishingShot.CastStep)
            yield return stand + way * along;
    }

    public static float Seconds(float distance)
    {
        var share = (distance - FishingReach.Nearest) / (LongThrow - FishingReach.Nearest);
        return ShortestSeconds + Math.Clamp(share, 0f, 1f) * (LongestSeconds - ShortestSeconds);
    }

    public static float Rise(float distance) => RiseLeast + RisePerCm * Math.Max(0f, distance);

    // Where the float is a share of the way through its flight: on the straight way, lifted by the arc.
    public static Vector3 PointAt(Vector3 from, Vector3 to, float rise, float share)
    {
        var done = Math.Clamp(share, 0f, 1f);
        return Vector3.Lerp(from, to, done) + new Vector3(0f, 0f, rise * ArcAt(done));
    }

    // A parabola from 0 at either end of the way to 1 halfway.
    public static float ArcAt(float share)
    {
        var done = Math.Clamp(share, 0f, 1f);
        return 4f * done * (1f - done);
    }
}
