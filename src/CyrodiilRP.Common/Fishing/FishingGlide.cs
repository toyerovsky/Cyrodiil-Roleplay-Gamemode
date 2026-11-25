using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// A smooth approach to a point that may move: each second the same share of what is left of the way, whatever the frame
// rate. The rate is how quickly: about 3 / rate seconds come within 5% of the way.
public static class FishingGlide
{
    public static float ShareOf(float rate, float seconds) =>
        seconds > 0f && rate > 0f ? 1f - MathF.Exp(-rate * seconds) : 0f;

    public static Vector3 Toward(Vector3 current, Vector3 wanted, float rate, float seconds) =>
        Vector3.Lerp(current, wanted, ShareOf(rate, seconds));

    public static float Toward(float current, float wanted, float rate, float seconds) =>
        current + (wanted - current) * ShareOf(rate, seconds);

    // A yaw in degrees, the shortest way round.
    public static float TurnToward(float yaw, float wanted, float rate, float seconds) =>
        yaw + FishingShot.Turn(wanted - yaw) * ShareOf(rate, seconds);
}
