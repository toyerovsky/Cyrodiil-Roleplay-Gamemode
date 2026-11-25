using System.Numerics;
using CyrodiilRP.Common.Fishing;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// The angler's own camera: set over the shoulder as the fishing starts, then gliding each frame to the shot the moment
// asks for, quicker to the bite, slower round to the catch, and given back to the game at the end.
[Service]
internal sealed partial class AnglerCamera(ICamera camera, ILogger logger)
{
    private const float BlendInSeconds = 0.7f;
    private const float BlendOutSeconds = 0.5f;
    private const float ShoulderGlide = 4f;
    private const float BiteGlide = 10f;
    private const float FightGlide = 3f;
    private const float CatchGlide = 2.5f;
    // A glide shorter than this is not worth setting the camera again.
    private const float StillWithin = 0.05f;
    // The camera refuses to look at the point it stands on.
    private const float NearestLook = 1f;

    private CameraFrame? _drawn;
    private float _glidesFrom;
    private bool _unusableTold;

    public void Begin(CameraFrame frame, float now)
    {
        if (!IsUsable(frame))
            return;

        camera.Set(frame.At, frame.LookAt, BlendInSeconds);
        _drawn = frame;
        _glidesFrom = now + BlendInSeconds;
    }

    public void Film(AnglerShot shot, CameraFrame wanted, float now, float seconds)
    {
        if (_drawn is not { } drawn || now < _glidesFrom || !IsUsable(wanted))
            return;

        var rate = RateOf(shot);
        var next = new CameraFrame(FishingGlide.Toward(drawn.At, wanted.At, rate, seconds), FishingGlide.Toward(drawn.LookAt, wanted.LookAt, rate, seconds));
        if (!IsUsable(next) || (Vector3.Distance(next.At, drawn.At) < StillWithin && Vector3.Distance(next.LookAt, drawn.LookAt) < StillWithin))
            return;

        camera.Set(next.At, next.LookAt);
        _drawn = next;
    }

    public void End()
    {
        if (_drawn is null)
            return;

        _drawn = null;
        camera.Reset(BlendOutSeconds);
    }

    // The game's camera throws on a point that is no number or a look at itself, which would stop the frame of every mod.
    private bool IsUsable(CameraFrame frame)
    {
        var usable = float.IsFinite(frame.At.X) && float.IsFinite(frame.At.Y) && float.IsFinite(frame.At.Z)
            && float.IsFinite(frame.LookAt.X) && float.IsFinite(frame.LookAt.Y) && float.IsFinite(frame.LookAt.Z)
            && Vector3.Distance(frame.At, frame.LookAt) >= NearestLook;
        if (!usable && !_unusableTold)
        {
            logger.LogWarning("The angler's camera was asked to stand at {At} looking at {LookAt}: it stays where it is.", frame.At, frame.LookAt);
            _unusableTold = true;
        }

        return usable;
    }

    private static float RateOf(AnglerShot shot) => shot switch
    {
        AnglerShot.Shoulder => ShoulderGlide,
        AnglerShot.Bite => BiteGlide,
        AnglerShot.Fight => FightGlide,
        AnglerShot.Catch => CatchGlide,
    };
}
