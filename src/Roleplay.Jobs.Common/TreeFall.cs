using System.Numerics;
using OblivionMP.SDK.Common.Props;

namespace Roleplay.Jobs.Common;

/// <summary>
/// How a felled tree comes down on each client within <paramref name="ViewDistance"/> centimeters of it: it tips away
/// from the chopper, faster as it goes, until <paramref name="FallDegrees"/> off upright after
/// <paramref name="FallSeconds"/>, lies there <paramref name="LieSeconds"/>, then sinks <paramref name="SinkDepth"/>
/// centimeters into the ground over <paramref name="SinkSeconds"/> and is gone. A bigger tree falls slower, by the
/// square root of its scale as a falling pole of that height would, and sinks deeper, by its scale.
/// </summary>
public readonly record struct TreeFall(float FallSeconds, float FallDegrees, float LieSeconds, float SinkSeconds,
    float SinkDepth, float ViewDistance)
{
    public float FallSecondsFor(float scale) => FallSeconds * MathF.Sqrt(scale);

    public float SecondsFor(float scale) => FallSecondsFor(scale) + LieSeconds + SinkSeconds;

    /// <summary>How far over the tree is, in degrees off upright, and how deep it has sunk, in centimeters.</summary>
    /// <param name="seconds">Since it was felled.</param>
    public (float Tilt, float Sink) PoseAt(float seconds, float scale)
    {
        var fall = FallSecondsFor(scale);
        var falling = fall > 0f ? Math.Clamp(seconds / fall, 0f, 1f) : 1f;
        var sinkingFor = seconds - fall - LieSeconds;
        var sinking = SinkSeconds > 0f ? Math.Clamp(sinkingFor / SinkSeconds, 0f, 1f) : sinkingFor >= 0f ? 1f : 0f;

        // Falling speeds up the whole way down; sinking eases in and out
        return (FallDegrees * falling * falling, SinkDepth * scale * sinking * sinking * (3f - 2f * sinking));
    }

    /// <summary>
    /// The tree's rotation (pitch, yaw and roll, Unreal's) when it stood as <paramref name="standing"/> and has tipped
    /// <paramref name="tilt"/> degrees over towards <paramref name="fallYaw"/>, about the foot of its trunk.
    /// </summary>
    public static Vector3 Tipped(Vector3 standing, float fallYaw, float tilt)
    {
        var (sin, cos) = MathF.SinCos(fallYaw * MathF.PI / 180f);
        // Turning about the horizontal line across the fall takes the top towards it
        var across = new Vector3(-sin, cos, 0f);
        var tip = Quaternion.CreateFromAxisAngle(across, tilt * MathF.PI / 180f);
        return UnrealRotator.FromQuaternion(tip * UnrealRotator.ToQuaternion(standing));
    }
}
