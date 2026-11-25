using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// The small fish swimming round a float in the water, as every game near the angler draws them: each on a loose circle
// of its own, swum into from the open water and fled from when a fish is hooked. Centimetres and seconds.
public static class ShoalMotion
{
    public const int Count = 5;

    public const float RadiusLeast = 100f;
    public const float RadiusMost = 320f;
    // A circle no nearer the angler than this share of the way from the angler to the float, and never tighter than
    // Tightest.
    public const float ShoreShare = 0.5f;
    public const float Tightest = 50f;
    public const float SpeedLeast = 30f;
    public const float SpeedMost = 75f;
    public const float DepthLeast = 25f;
    public const float DepthMost = 60f;
    public const int LengthLeast = 12;
    public const int LengthMost = 34;

    // A circle swells and shrinks by this share of its radius, once in a sway's time.
    public const float Sway = 0.25f;
    public const float SwayLeast = 5f;
    public const float SwayMost = 10f;

    // The fish come from this far out in the open water, this much deeper.
    public const float ArriveFrom = 450f;
    public const float ArriveDeeper = 40f;

    public const float FleeSpeed = 320f;
    public const float FleeSink = 50f;
    public const float FleeSeconds = 2f;

    // Where a fish heads: the point of its circle this much later.
    public const float AheadSeconds = 0.5f;

    public static ShoalSwimmer Pick(Random random, float room) => new(
        Math.Min(Between(random, RadiusLeast, RadiusMost), Math.Max(room, Tightest)),
        Between(random, SpeedLeast, SpeedMost),
        Clockwise: random.Next(2) == 0,
        Between(random, DepthLeast, DepthMost),
        Between(random, 0f, MathF.Tau),
        Between(random, SwayLeast, SwayMost),
        random.Next(LengthLeast, LengthMost + 1));

    public static Vector3 At(Vector3 center, ShoalSwimmer swimmer, float seconds)
    {
        var swum = swimmer.Speed * seconds / swimmer.Radius;
        var angle = swimmer.Phase + (swimmer.Clockwise ? -swum : swum);
        var radius = swimmer.Radius * (1f + Sway * MathF.Sin(MathF.Tau * seconds / swimmer.SwaySeconds + swimmer.Phase));
        return center + new Vector3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, -swimmer.Depth);
    }

    // Out in the open water past where it starts its circle, away being the way from the angler to the float.
    public static Vector3 ArrivingFrom(Vector3 center, ShoalSwimmer swimmer, Vector3 away) =>
        At(center, swimmer, 0f) + Flat(away) * ArriveFrom - new Vector3(0f, 0f, ArriveDeeper);

    // On out from the float and down, for this many seconds more.
    public static Vector3 Flee(Vector3 center, Vector3 from, float seconds) =>
        from + Flat(from - center) * (FleeSpeed * seconds) - new Vector3(0f, 0f, FleeSink * seconds);

    public static Vector3 Flat(Vector3 way)
    {
        var flat = way with { Z = 0f };
        return flat.LengthSquared() > 0f ? Vector3.Normalize(flat) : Vector3.UnitX;
    }

    private static float Between(Random random, float least, float most) => least + random.NextSingle() * (most - least);
}
