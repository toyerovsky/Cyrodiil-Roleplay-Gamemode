using System.Numerics;

namespace Roleplay.Jobs.Common;

/// <summary>
/// A piece of stone thrown off a hit, or out of a node that bursts: it leaves with a velocity, tumbles under gravity, and
/// comes to rest where it falls to <paramref name="GroundZ"/>, the height of the ground it lands on.
/// </summary>
public readonly record struct StoneChip(Vector3 Start, Vector3 Velocity, Vector3 Spin, float GroundZ)
{
    public const float Gravity = 980f;

    /// <summary>A chip flying away from <paramref name="from"/> (the miner), up and out to one side by the rolls.</summary>
    /// <param name="at">Where the hit landed; the chip starts a little off it, on the miner's side.</param>
    /// <param name="spread">0 to 1, which way to the side: 0.5 is straight away from the miner.</param>
    /// <param name="lift">0 to 1, how steeply up.</param>
    public static StoneChip Thrown(Vector3 at, Vector2 from, float speed, float spread, float lift, float spin, float groundZ)
    {
        var away = new Vector2(at.X - from.X, at.Y - from.Y);
        away = away.LengthSquared() < 1f ? Vector2.UnitX : Vector2.Normalize(away);

        // Out to either side of straight away, up to 60 degrees
        var side = (spread - 0.5f) * 2f * MathF.PI / 3f;
        var (sin, cos) = MathF.SinCos(side);
        var flat = new Vector2(away.X * cos - away.Y * sin, away.X * sin + away.Y * cos);

        // From 30 to 70 degrees up
        var (up, along) = MathF.SinCos((30f + lift * 40f) * MathF.PI / 180f);
        var start = at + new Vector3(-away * 30f, 20f);
        return new StoneChip(start with { Z = MathF.Max(start.Z, groundZ) }, new Vector3(flat * along, up) * speed,
            new Vector3(spin, spin * 0.7f, spin * 0.4f), groundZ);
    }

    /// <summary>A piece flying out of a node bursting at <paramref name="center"/>, any way round, by the rolls.</summary>
    /// <param name="around">0 to 1, which way round.</param>
    /// <param name="lift">0 to 1, how steeply up: 20 to 75 degrees.</param>
    public static StoneChip Burst(Vector3 center, float speed, float around, float lift, float spin, float groundZ)
    {
        var (sin, cos) = MathF.SinCos(around * 2f * MathF.PI);
        var (up, along) = MathF.SinCos((20f + lift * 55f) * MathF.PI / 180f);
        var velocity = new Vector3(cos * along, sin * along, up) * speed;
        return new StoneChip(center with { Z = MathF.Max(center.Z, groundZ) }, velocity, new Vector3(spin, spin * 0.6f, spin * 0.3f), groundZ);
    }

    /// <summary>When it lands: the later time its fall reaches the ground.</summary>
    public float LandsAt
    {
        get
        {
            var above = Start.Z - GroundZ;
            return (Velocity.Z + MathF.Sqrt(Velocity.Z * Velocity.Z + 2f * Gravity * MathF.Max(0f, above))) / Gravity;
        }
    }

    /// <summary>Where it is: in flight, or lying where it landed.</summary>
    public Vector3 PositionAt(float seconds)
    {
        var t = MathF.Min(seconds, LandsAt);
        var position = Start + Velocity * t - new Vector3(0, 0, Gravity * t * t / 2f);
        return position with { Z = MathF.Max(position.Z, GroundZ) };
    }

    /// <summary>Pitch, yaw and roll in degrees: it tumbles at its spin until it lands, then lies still.</summary>
    public Vector3 RotationAt(float seconds) => Spin * MathF.Min(seconds, LandsAt);
}
