using System.Numerics;

namespace CyrodiilRP.Common.Fishing;

// The angler's own camera at each moment of the fishing. Stand is the feet, yaw the way the angler faces. Centimetres.
public static class AnglerFraming
{
    // At the bite the camera comes this far toward the float, and this much lower, no nearer the water than the last:
    // a step, not a share of the way, so a long cast still keeps the angler and the rod in the shot.
    public const float BiteForward = 80f;
    public const float BiteLower = 60f;
    public const float BiteAboveWater = 60f;

    // In the fight it looks from the shoulder at a point between the angler's chest and the float, near the float.
    public const float FightLookShare = 0.8f;
    public const float ChestUp = 130f;

    // The catch is shown from the angler's front right, a little over the chest, looking between the right hand and the
    // face, so the head stays in the shot.
    public const float CatchAhead = 230f;
    public const float CatchRight = 120f;
    public const float CatchUp = 155f;
    public const float CatchLookUp = 145f;
    public const float HandAhead = 35f;
    public const float HandRight = 25f;
    public const float HandUp = 120f;

    public static CameraFrame Shoulder(Vector3 stand, float yaw, float waterZ, Vector3 lookAt) =>
        new(FishingShot.CameraAt(stand, yaw, waterZ), lookAt);

    public static CameraFrame Bite(CameraFrame shoulder, Vector3 floatAt)
    {
        var nearer = shoulder.At + Flat(floatAt - shoulder.At) * BiteForward;
        var z = Math.Max(nearer.Z - BiteLower, floatAt.Z + BiteAboveWater);
        return new CameraFrame(nearer with { Z = z }, floatAt);
    }

    // The way along the ground, or none for a point straight above or below.
    private static Vector3 Flat(Vector3 way)
    {
        var flat = way with { Z = 0f };
        return flat.LengthSquared() > 0f ? Vector3.Normalize(flat) : Vector3.Zero;
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
        return new CameraFrame(at, HandAt(stand, yaw) with { Z = stand.Z + CatchLookUp });
    }

    // About where the right hand holds the catch up.
    public static Vector3 HandAt(Vector3 stand, float yaw) =>
        stand + FishingShot.Forward(yaw) * HandAhead + FishingShot.Right(yaw) * HandRight + new Vector3(0f, 0f, HandUp);
}
