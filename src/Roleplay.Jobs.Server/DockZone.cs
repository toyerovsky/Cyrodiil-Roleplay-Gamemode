using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>A crate deposit: (X, Y, Z) is the middle of its floor, Yaw its turn in degrees.</summary>
public sealed record DockZone(string Id, DockZoneKind Kind, float X, float Y, float Z, float Yaw)
{
    // A crate resting a little under the floor the deposit was placed on (a slope, rounding) is still in it
    private const float BelowFloor = 50f;

    public Vector3 Floor => new(X, Y, Z);

    /// <summary>Whether the point is inside the deposit, a cube <paramref name="size"/> on a side standing on its
    /// floor, turned by its yaw.</summary>
    public bool Holds(Vector3 point, float size)
    {
        var radians = -Yaw * MathF.PI / 180f;
        var dx = point.X - X;
        var dy = point.Y - Y;
        var along = dx * MathF.Cos(radians) - dy * MathF.Sin(radians);
        var across = dx * MathF.Sin(radians) + dy * MathF.Cos(radians);
        var half = size / 2f;
        return MathF.Abs(along) <= half && MathF.Abs(across) <= half && point.Z - Z >= -BelowFloor && point.Z - Z <= size;
    }
}
