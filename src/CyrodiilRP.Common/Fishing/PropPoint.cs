using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// Where a point of a mesh is in the world, from where the game has the mesh: its origin, and its pitch, yaw and roll in
// degrees, turned as the game turns a prop (yaw about Z, then pitch, then roll).
public static class PropPoint
{
    private const float Radians = MathF.PI / 180f;

    public static Vector3 At(Vector3 origin, Vector3 rotation, Vector3 local)
    {
        var pitchSin = MathF.Sin(rotation.X * Radians);
        var pitchCos = MathF.Cos(rotation.X * Radians);
        var yawSin = MathF.Sin(rotation.Y * Radians);
        var yawCos = MathF.Cos(rotation.Y * Radians);
        var rollSin = MathF.Sin(rotation.Z * Radians);
        var rollCos = MathF.Cos(rotation.Z * Radians);
        var ahead = new Vector3(pitchCos * yawCos, pitchCos * yawSin, pitchSin);
        var right = new Vector3(rollSin * pitchSin * yawCos - rollCos * yawSin, rollSin * pitchSin * yawSin + rollCos * yawCos, -rollSin * pitchCos);
        var up = new Vector3(-(rollCos * pitchSin * yawCos + rollSin * yawSin), yawCos * rollSin - rollCos * pitchSin * yawSin, rollCos * pitchCos);
        return origin + ahead * local.X + right * local.Y + up * local.Z;
    }
}
