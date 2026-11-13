using System.Numerics;

namespace Roleplay.Jobs.Common;

/// <summary>
/// A piece thrown off a hit (stone off rock, bark off a trunk), or out of a node that bursts: it leaves with a velocity
/// and tumbles under gravity, falling on through the ground until it is taken away. It starts no lower than
/// <paramref name="GroundZ"/>.
/// </summary>
public readonly record struct StoneChip(Vector3 Start, Vector3 Velocity, Vector3 Spin, float GroundZ)
{
    public const float Gravity = 980f;

    /// <summary>A chip flying back towards <paramref name="from"/> (who struck), up and out to one side by the rolls: it
    /// bounces off the face struck, and lands by their feet, the ground height it is given.</summary>
    /// <param name="at">Where the hit landed; the chip starts a little off it, on the striker's side.</param>
    /// <param name="spread">0 to 1, which way to the side: 0.5 is straight back at the striker.</param>
    /// <param name="lift">0 to 1, how steeply up.</param>
    public static StoneChip Thrown(Vector3 at, Vector2 from, float speed, float spread, float lift, float spin, float groundZ)
    {
        var back = new Vector2(from.X - at.X, from.Y - at.Y);
        back = back.LengthSquared() < 1f ? -Vector2.UnitX : Vector2.Normalize(back);

        // Out to either side of straight back, up to 60 degrees
        var side = (spread - 0.5f) * 2f * MathF.PI / 3f;
        var (sin, cos) = MathF.SinCos(side);
        var flat = new Vector2(back.X * cos - back.Y * sin, back.X * sin + back.Y * cos);

        // From 30 to 70 degrees up
        var (up, along) = MathF.SinCos((30f + lift * 40f) * MathF.PI / 180f);
        var start = at + new Vector3(back * 30f, 20f);
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

    /// <summary>Where it is after the given seconds of flight.</summary>
    public Vector3 PositionAt(float seconds) => Start + Velocity * seconds - new Vector3(0, 0, Gravity * seconds * seconds / 2f);

    /// <summary>Pitch, yaw and roll in degrees: it tumbles at its spin all the way.</summary>
    public Vector3 RotationAt(float seconds) => Spin * seconds;
}
