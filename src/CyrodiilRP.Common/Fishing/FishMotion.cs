using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// Where a fish near a float swims, as every game near the angler draws it. Yaw in degrees as FishingShot's, centimetres
// and seconds.
public static class FishMotion
{
    private const float HalfTurn = 180f;
    private const float QuarterTurn = 90f;
    private const float FullTurn = 360f;

    // The fish mesh is this long along its own Y at scale 1.
    public const float MeshLength = 66f;
    public const int ShortestShown = 10;
    public const int LongestShown = 130;

    public const float Depth = 40f;
    public const float CircleRadius = 70f;
    public const float CircleSeconds = 6f;

    // A nibble darts in to stop this short of the float, this far under it.
    public const float DartShort = 15f;
    public const float DartDepth = 15f;

    public const float JumpSeconds = 0.8f;
    public const float JumpLength = 120f;
    public const float JumpHeight = 70f;
    // Nose up as it leaves the water, down as it comes back.
    public const float JumpTilt = 35f;

    public const float EscapeSpeed = 250f;
    public const float EscapeSink = 60f;

    public static float ScaleFor(int lengthCm) => Math.Clamp(lengthCm, ShortestShown, LongestShown) / MeshLength;

    // Round the float under the water, once in the circle's time.
    public static Vector3 CircleAt(Vector3 center, float seconds)
    {
        var angle = MathF.Tau * seconds / CircleSeconds;
        return center + new Vector3(MathF.Cos(angle) * CircleRadius, MathF.Sin(angle) * CircleRadius, -Depth);
    }

    // Along the circle, a quarter turn on from the way out to the fish.
    public static float CircleHeading(float seconds) => FishingShot.Turn(seconds / CircleSeconds * FullTurn + QuarterTurn);

    // Just short of the float on the side the fish comes from.
    public static Vector3 DartAt(Vector3 center, Vector3 from)
    {
        var away = new Vector3(from.X - center.X, from.Y - center.Y, 0f);
        var side = away.LengthSquared() > 0f ? Vector3.Normalize(away) : Vector3.UnitX;
        return center + side * DartShort - new Vector3(0f, 0f, DartDepth);
    }

    // Out of the water at the start and back into it at the end, along the yaw.
    public static Vector3 JumpAt(Vector3 start, float yaw, float share)
    {
        var done = Math.Clamp(share, 0f, 1f);
        return start + FishingShot.Forward(yaw) * (JumpLength * done) + new Vector3(0f, 0f, JumpHeight * CastFlight.ArcAt(done));
    }

    public static float JumpTiltAt(float share) => JumpTilt * (1f - 2f * Math.Clamp(share, 0f, 1f));

    public static Vector3 EscapeAt(Vector3 start, float yaw, float seconds)
    {
        var gone = Math.Max(0f, seconds);
        return start + FishingShot.Forward(yaw) * (EscapeSpeed * gone) - new Vector3(0f, 0f, Depth + EscapeSink * gone);
    }

    // The way from one point to another on the ground plane, or the yaw given when they stand one over the other.
    public static float Heading(Vector3 from, Vector3 to, float otherwise)
    {
        var x = to.X - from.X;
        var y = to.Y - from.Y;
        return x * x + y * y > 0f ? MathF.Atan2(y, x) * HalfTurn / MathF.PI : otherwise;
    }
}
