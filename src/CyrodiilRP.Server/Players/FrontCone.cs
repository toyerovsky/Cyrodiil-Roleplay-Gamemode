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

        var yaw = yawDegrees * DegreesToRadians;
        var facing = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
        return Vector2.Dot(Vector2.Normalize(flat), facing) >= MathF.Cos(coneDegrees / 2 * DegreesToRadians);
    }
}
