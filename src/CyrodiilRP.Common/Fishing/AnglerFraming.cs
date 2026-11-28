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

    // Just over the angler's head, clear of the body and what it carries: the camera is reached from here.
    public const float OverHeadUp = 195f;

    // The camera stops this far short of what stands in its way.
    public const float ObstacleClear = 25f;

    // Behind the angler on the way to what it looks at, so the angler stays in the shot wherever the cast went.
    public static CameraFrame Shoulder(Vector3 stand, float yaw, float waterZ, Vector3 lookAt, CameraRung rung) =>
        new(FishingShot.CameraAt(stand, FishMotion.Heading(stand, lookAt, yaw), waterZ, rung), lookAt);

    // The camera looks no steeper down than this, and this share of the way's length over what it frames: a float near
    // the bank does not turn it onto the angler's feet, and the float sits under the middle with room for the rod's tip.
    public const float SteepestDegrees = 35f;
    public const float LookOver = 0.1f;
    private const float DegreesToRadians = MathF.PI / 180f;

    public static CameraFrame Level(CameraFrame frame)
    {
        var way = frame.LookAt - frame.At;
        var flat = MathF.Sqrt(way.X * way.X + way.Y * way.Y);
        if (flat < 1f)
            return frame;

        var over = frame.LookAt.Z + flat * LookOver;
        var lowest = frame.At.Z - flat * MathF.Tan(SteepestDegrees * DegreesToRadians);
        return frame with { LookAt = frame.LookAt with { Z = Math.Max(over, lowest) } };
    }

    public static Vector3 OverHead(Vector3 stand) => stand + new Vector3(0f, 0f, OverHeadUp);

    // The point on the way from one point to an obstacle that keeps clear of it, the first point itself for one nearer
    // than that.
    public static Vector3 Before(Vector3 from, Vector3 obstacle)
    {
        var way = obstacle - from;
        var length = way.Length();
        return length <= ObstacleClear ? from : from + way * ((length - ObstacleClear) / length);
    }

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
    public static CameraFrame Fight(Vector3 stand, float yaw, float waterZ, Vector3 floatAt, CameraRung rung)
    {
        var toFloat = FishMotion.Heading(stand, floatAt, yaw);
        var chest = stand + new Vector3(0f, 0f, ChestUp);
        return new CameraFrame(FishingShot.CameraAt(stand, toFloat, waterZ, rung), Vector3.Lerp(chest, floatAt, FightLookShare));
    }

    // The harder the line strains, the nearer the camera comes to what it looks at and the narrower it sees. Centimetres
    // and degrees at a line about to snap.
    public const float StrainPushIn = 90f;
    public const float StrainNarrows = 10f;
    public const float BiteNarrows = 4f;

    // With the line in the red the camera trembles: this far at the most, to and fro this often a second.
    public const float ShakeMost = 1.5f;
    public const float ShakeHertz = 11f;
    private const float ShakeUpFaster = 1.3f;

    // No nearer what it looks at than half the way there.
    public static CameraFrame Strained(CameraFrame frame, float tension)
    {
        var way = frame.LookAt - frame.At;
        var length = way.Length();
        var step = Math.Min(StrainPushIn * Math.Clamp(tension, 0f, 1f), length / 2f);
        return length > 0f ? frame with { At = frame.At + way / length * step } : frame;
    }

    // How deep in the red the line is, 0 at where the red begins and 1 where it snaps.
    public static float RedShare(float tension, float warn) =>
        warn < 1f ? Math.Clamp((tension - warn) / (1f - warn), 0f, 1f) : 0f;

    // Aside and up and down, each on a beat of its own.
    public static Vector3 Shake(CameraFrame frame, float seconds, float strength)
    {
        var far = Math.Clamp(strength, 0f, 1f) * ShakeMost;
        var way = frame.LookAt - frame.At;
        var aside = new Vector3(way.Y, -way.X, 0f);
        var side = aside.LengthSquared() > 0f ? Vector3.Normalize(aside) : Vector3.Zero;
        var beat = seconds * MathF.Tau * ShakeHertz;
        return side * (MathF.Sin(beat) * far) + new Vector3(0f, 0f, MathF.Sin(beat * ShakeUpFaster + 1f) * far);
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
