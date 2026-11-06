using System.Numerics;

namespace CyrodiilRP.Server.Players;

// The game's yaw is in degrees from +X towards +Y, positions in centimetres.
internal static class FrontCone
{
    private const float DegreesToRadians = MathF.PI / 180f;

    // Closer than this, a player stands inside the other and has no direction.
    private const float Touching = 1f;

    public static bool Contains(Vector3 origin, float yawDegrees, Vector3 point, float reach, float coneDegrees)
    {
        var offset = point - origin;
        if (offset.Length() > reach)
            return false;

        var flat = new Vector2(offset.X, offset.Y);
        if (flat.Length() < Touching)
            return true;

        return Vector2.Dot(Vector2.Normalize(flat), Face(yawDegrees)) >= MathF.Cos(coneDegrees / 2 * DegreesToRadians);
    }

    // The point a distance ahead of where the yaw faces, level with the origin.
    public static Vector3 FindAhead(Vector3 origin, float yawDegrees, float distance)
    {
        var facing = Face(yawDegrees);
        return origin + new Vector3(facing.X, facing.Y, 0f) * distance;
    }

    private static Vector2 Face(float yawDegrees)
    {
        var yaw = yawDegrees * DegreesToRadians;
        return new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
    }
}
