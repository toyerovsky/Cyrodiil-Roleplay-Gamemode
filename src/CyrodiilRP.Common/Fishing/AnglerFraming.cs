using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// The angler's own camera at each moment of the fishing. Stand is the feet, yaw the way the angler faces. Centimetres.
public static class AnglerFraming
{
    // At the bite the camera comes this share of the way to the float, and this much lower, no nearer the water than
    // the last.
    public const float BiteCloser = 1f / 3f;
    public const float BiteLower = 100f;
    public const float BiteAboveWater = 60f;

    // In the fight it looks from the shoulder at a point between the angler's chest and the float, near the float.
    public const float FightLookShare = 0.8f;
    public const float ChestUp = 130f;

    // The catch is shown from the angler's front right at chest height, looking at the right hand.
    public const float CatchAhead = 170f;
    public const float CatchRight = 110f;
    public const float CatchUp = 140f;
    public const float HandAhead = 35f;
    public const float HandRight = 25f;
    public const float HandUp = 120f;

    public static CameraFrame Shoulder(Vector3 stand, float yaw, float waterZ, Vector3 lookAt) =>
        new(FishingShot.CameraAt(stand, yaw, waterZ), lookAt);

    public static CameraFrame Bite(CameraFrame shoulder, Vector3 floatAt)
    {
        var nearer = Vector3.Lerp(shoulder.At, floatAt, BiteCloser);
        var z = Math.Max(nearer.Z - BiteLower, floatAt.Z + BiteAboveWater);
        return new CameraFrame(nearer with { Z = z }, floatAt);
    }

    // Over the shoulder turned to the float, so both stay in view as the fish drags it aside.
    public static CameraFrame Fight(Vector3 stand, float yaw, float waterZ, Vector3 floatAt)
    {
        var toFloat = FishMotion.Heading(stand, floatAt, yaw);
        var chest = stand + new Vector3(0f, 0f, ChestUp);
        return new CameraFrame(FishingShot.CameraAt(stand, toFloat, waterZ), Vector3.Lerp(chest, floatAt, FightLookShare));
    }

    public static CameraFrame Catch(Vector3 stand, float yaw)
    {
        var at = stand + FishingShot.Forward(yaw) * CatchAhead + FishingShot.Right(yaw) * CatchRight + new Vector3(0f, 0f, CatchUp);
        return new CameraFrame(at, HandAt(stand, yaw));
    }

    // About where the right hand holds the catch up.
    public static Vector3 HandAt(Vector3 stand, float yaw) =>
        stand + FishingShot.Forward(yaw) * HandAhead + FishingShot.Right(yaw) * HandRight + new Vector3(0f, 0f, HandUp);
}
