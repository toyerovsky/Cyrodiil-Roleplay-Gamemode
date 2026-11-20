using System.Numerics;

namespace Roleplay.Jobs.Common;

/// <summary>Whether a point is ahead of a character. The game's yaw is in degrees from +X towards +Y, positions in centimetres.</summary>
public static class Facing
{
    private const float DegreesToRadians = MathF.PI / 180f;

    // Closer than this, the point is where the character stands and has no direction
    private const float Touching = 1f;

    /// <summary>True when <paramref name="point"/> is within <paramref name="reach"/> of <paramref name="origin"/> and
    /// inside a cone of <paramref name="coneDegrees"/> around where <paramref name="yawDegrees"/> faces.</summary>
    public static bool IsAhead(Vector3 origin, float yawDegrees, Vector3 point, float reach, float coneDegrees)
    {
        var offset = point - origin;
        if (offset.Length() > reach)
            return false;

        var flat = new Vector2(offset.X, offset.Y);
        if (flat.Length() < Touching)
            return true;

        var yaw = yawDegrees * DegreesToRadians;
        var facing = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
        return Vector2.Dot(Vector2.Normalize(flat), facing) >= MathF.Cos(coneDegrees / 2f * DegreesToRadians);
    }
}
