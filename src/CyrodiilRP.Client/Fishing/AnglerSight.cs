using System.Numerics;
using CyrodiilRP.Common.Fishing;
using OblivionMP.SDK;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// Where the angler's camera can stand: the first of the places over the shoulder that is reached from over the angler's
// head and sees what the camera looks at, and any place brought nearer when something stands in the way to it.
[Service]
internal sealed partial class AnglerSight(ICamera camera)
{
    private const float LooksEvery = 0.2f;
    // Another place has to stay the clear one this long before the camera moves to it, so a post the float drifts
    // behind does not swing the camera to and fro.
    private const float SettlesIn = 0.4f;

    private int _rung;
    private int _wanted;
    private float _wantedSince;
    private float _lookedAt = float.NegativeInfinity;
    private bool _settled;

    public CameraRung Rung => FishingShot.CameraRungs[_rung];

    public void Begin()
    {
        _rung = 0;
        _wanted = 0;
        _settled = false;
        _lookedAt = float.NegativeInfinity;
    }

    public bool IsDue(float now) => now - _lookedAt >= LooksEvery;

    // shots: the camera's frame on each rung of FishingShot.CameraRungs, in their order. The first look of a fishing
    // takes its place at once.
    public void Look(IReadOnlyList<CameraFrame> shots, Vector3 over, float now)
    {
        _lookedAt = now;
        var clear = FindClear(shots, over);
        if (!_settled || clear == _rung)
        {
            _rung = clear;
            _wanted = clear;
            _settled = true;
            return;
        }

        if (clear != _wanted)
        {
            _wanted = clear;
            _wantedSince = now;
            return;
        }

        if (now - _wantedSince >= SettlesIn)
            _rung = clear;
    }

    // The frame with its camera brought nearer when something stands between it and the point it is reached from.
    public CameraFrame Reach(Vector3 from, CameraFrame frame) =>
        FindObstacle(from, frame.At) is { } obstacle ? frame with { At = AnglerFraming.Before(from, obstacle) } : frame;

    // The usual place when none is clear: Reach then brings it as near as it has to.
    private int FindClear(IReadOnlyList<CameraFrame> shots, Vector3 over)
    {
        for (var index = 0; index < shots.Count; index++)
        {
            if (IsClear(over, shots[index]))
                return index;
        }

        return 0;
    }

    // The line of sight stops short of what is looked at, which is not in its own way.
    private bool IsClear(Vector3 over, CameraFrame frame) =>
        FindObstacle(over, frame.At) is null && FindObstacle(frame.At, AnglerFraming.Before(frame.At, frame.LookAt)) is null;

    // The game's trace throws on a point that is no number, which would stop the frame of every mod. Such a frame is
    // passed on as it is: AnglerCamera refuses it and says so.
    private Vector3? FindObstacle(Vector3 from, Vector3 to) =>
        IsPoint(from) && IsPoint(to) ? camera.FindObstacle(from, to) : null;

    private static bool IsPoint(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);
}
