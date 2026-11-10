using System.Numerics;

namespace Roleplay.Jobs.Server;

/// <summary>A carryable lying in the world, as <see cref="CarryableStore"/> keeps it: its kind by name, where its origin
/// is and which way it faces (yaw in degrees).</summary>
public sealed record Carryable(string Id, string Kind, float X, float Y, float Z, float Yaw)
{
    public Vector3 Position => new(X, Y, Z);
}
