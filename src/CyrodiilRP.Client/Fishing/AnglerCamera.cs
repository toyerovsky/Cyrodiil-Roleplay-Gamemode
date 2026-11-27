using System.Numerics;
using CyrodiilRP.Common.Fishing;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// The angler's own camera: set over the shoulder as the fishing starts, then gliding each frame to the shot the moment
// asks for, quicker to the bite, slower round to the catch, and given back to the game at the end. The game's own view
// is held in third person meanwhile, so the angler is drawn whole.
[Service]
internal sealed partial class AnglerCamera(ICamera camera, ILogger logger)
{
    private const float BlendInSeconds = 0.7f;
    private const float BlendOutSeconds = 0.5f;
    private const float ShoulderGlide = 4f;
    private const float BiteGlide = 10f;
    private const float FightGlide = 3f;
    private const float CatchGlide = 2.5f;
    private const float NarrowGlide = 3f;
    // A view this much narrower or wider than the one set is not worth setting again. Degrees.
    private const float SameAngle = 0.05f;
    // A glide shorter than this is not worth setting the camera again.
    private const float StillWithin = 0.05f;
    // The camera refuses to look at the point it stands on.
    private const float NearestLook = 1f;

    private CameraFrame? _drawn;
    private CameraFrame _placed;
    private float _gameAngle;
    private float _narrowed;
    private float _narrowedPlaced;
    private float _glidesFrom;
    private bool _unusableTold;
    private bool _viewLocked;
    private float? _unlocksAt;

    public void Begin(CameraFrame frame, float now)
    {
        if (!IsUsable(frame))
            return;

        // Read while the view is still the game's: the angle the player chose, which the strain narrows from.
        _gameAngle = camera.FieldOfView ?? 0f;
        LockView();
        camera.Set(frame.At, frame.LookAt, BlendInSeconds);
        _drawn = frame;
        _placed = frame;
        _narrowed = 0f;
        _narrowedPlaced = 0f;
        _glidesFrom = now + BlendInSeconds;
    }

    // narrow: how many degrees narrower than the player's own view the moment asks for. shake: where the camera
    // trembles to from its place this frame, which no glide smooths away.
    public void Film(AnglerShot shot, CameraFrame wanted, float narrow, Vector3 shake, float now, float seconds)
    {
        if (_drawn is not { } drawn || now < _glidesFrom || !IsUsable(wanted))
            return;

        var rate = RateOf(shot);
        var next = new CameraFrame(FishingGlide.Toward(drawn.At, wanted.At, rate, seconds), FishingGlide.Toward(drawn.LookAt, wanted.LookAt, rate, seconds));
        var placed = next with { At = next.At + shake };
        _narrowed = FishingGlide.Toward(_narrowed, narrow, NarrowGlide, seconds);
        var still = Vector3.Distance(placed.At, _placed.At) < StillWithin && Vector3.Distance(placed.LookAt, _placed.LookAt) < StillWithin
            && Math.Abs(_narrowed - _narrowedPlaced) < SameAngle;
        if (still || !IsUsable(placed))
            return;

        // Without the game's angle the view keeps it, whatever the moment asks for.
        camera.Set(placed.At, placed.LookAt, 0f, _gameAngle > 0f ? Math.Max(1f, _gameAngle - _narrowed) : 0f);
        _drawn = next;
        _placed = placed;
        _narrowedPlaced = _narrowed;
    }

    public void End()
    {
        if (_drawn is null)
            return;

        _drawn = null;
        camera.Reset(BlendOutSeconds);
        _unlocksAt = Time.Elapsed + BlendOutSeconds;
    }

    private void Stop() => UnlockView();

    // The view the player chose comes back once the camera has glided home, so first person's arms do not show on the way.
    private void Update()
    {
        if (_unlocksAt is { } at && Time.Elapsed >= at)
            UnlockView();
    }

    // The game draws a character seen in first person as its arms alone, under a camera of the mode's own too.
    private void LockView()
    {
        _unlocksAt = null;
        if (_viewLocked)
            return;

        _viewLocked = camera.LockView(CameraView.ThirdPerson);
        if (!_viewLocked)
            logger.LogWarning("The game did not take the third person view for the angler's camera.");
    }

    private void UnlockView()
    {
        _unlocksAt = null;
        if (!_viewLocked)
            return;

        _viewLocked = false;
        camera.UnlockView();
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
