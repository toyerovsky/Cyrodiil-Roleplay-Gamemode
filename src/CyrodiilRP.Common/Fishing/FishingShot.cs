using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// The angler's camera and aim. Yaw in degrees as the game's: 0 faces +X, 90 faces +Y, which is to the right.
public static class FishingShot
{
    private const float HalfTurn = 180f;
    private const float FullTurn = 360f;

    // Over the angler's right shoulder, high enough to see the float over the reeds. Centimetres.
    public const float CameraBack = 220f;
    public const float CameraRight = 90f;
    public const float CameraUp = 340f;

    // From the farthest the rod reaches to the nearest, a step of this many centimetres at a time.
    public const float CastStep = 50f;

    // How high above a point a look for water starts, and how far down it goes.
    public const float LookFromAbove = 300f;
    public const float LookDown = 1500f;

    public static Vector3 Forward(float yaw)
    {
        var radians = yaw * MathF.PI / HalfTurn;
        return new Vector3(MathF.Cos(radians), MathF.Sin(radians), 0f);
    }

    public static Vector3 Right(float yaw) => Forward(yaw + HalfTurn / 2);

    // A turn as the shortest way round, from -180 to 180 degrees.
    public static float Turn(float degrees) => ((degrees + HalfTurn) % FullTurn + FullTurn) % FullTurn - HalfTurn;

    // Where the cast goes: the way the player looks, kept within the aim's reach either side of the way the angler faces.
    public static float AimYaw(float facingYaw, float lookYaw) =>
        facingYaw + Math.Clamp(Turn(lookYaw - facingYaw), -FishingReach.AimDegrees, FishingReach.AimDegrees);

    public static Vector3 CameraAt(Vector3 stand, float facingYaw, float waterZ)
    {
        var behind = stand - Forward(facingYaw) * CameraBack + Right(facingYaw) * CameraRight;
        return behind with { Z = waterZ + CameraUp };
    }

    // The points a cast may land on along a yaw, from the farthest given down to the nearest.
    public static IEnumerable<Vector3> CastPoints(Vector3 stand, float yaw, float farthest)
    {
        for (var reach = farthest; reach >= FishingReach.Nearest; reach -= CastStep)
            yield return stand + Forward(yaw) * reach;
    }
}
