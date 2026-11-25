using System.Diagnostics;
using System.Numerics;
using OblivionMP.SDK;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Camera;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Building;

// Architect mode's view, in place of the game's camera (ICamera.Set): it looks down at a point on the ground from a
// distance, which WASD moves, the right mouse button turns round and tilts and the wheel brings nearer. The point stays
// over the plot, or near where marking began. Look around (B) gives the game its camera back until B again. Update
// thread only.
[Service]
internal sealed partial class ArchitectCamera(ICamera camera, ILocalProps props, BuildingProps building, ILogger logger)
{
    private const float GlideSeconds = 0.6f;

    // Degrees above the horizon, negative looks down.
    private const float StartPitch = -55f;
    private const float LeastPitch = -85f;
    private const float MostPitch = -10f;

    // From the point looked at, in centimetres.
    private const float LeastDistance = 300f;
    private const float MostDistance = 8000f;
    private const float MarkingDistance = 2500f;

    // A plot is seen whole from its longer side and a fifth more.
    private const float PlotView = 1.2f;

    // How much nearer or further one notch of the wheel brings the view.
    private const float ZoomStep = 1.15f;

    private const float DegreesPerPixel = 0.25f;

    // How far WASD moves the point in a second, as a share of the distance, so a far view moves faster.
    private const float PanPerSecond = 0.9f;

    // A frame longer than this (a hitch) moves the view as if it were this long.
    private const double MostFrameSeconds = 0.1;

    // How far past the plot's edge the point may go, and from where marking began, in centimetres.
    private const float PlotMargin = 1000f;
    private const float MarkingReach = 12000f;

    private const float Reach = 20_000f;
    private const float FallbackAhead = 1000f;

    private bool _active;
    private bool _paused;
    private long _plotId;
    private Vector3 _focus;
    private Vector3 _home;
    private float _yaw;
    private float _pitch;
    private float _distance;
    private Vector2 _pan;
    private long _lastTick;

    // Raised after the view moved, so what the mouse carries follows it.
    public event Action? Moved;

    public bool IsActive => _active && !_paused;

    // Ahead on the ground, as the view looks.
    public Vector3 Ahead => new CameraLook(0f, _yaw).Ahead;

    // Over the plot, or (0) where the player looks while choosing or marking one. Already open, choosing or marking keeps
    // the view where it is.
    public void Begin(long plotId)
    {
        if (plotId == 0 && _active)
        {
            _plotId = 0;
            _home = _focus;
            return;
        }

        _plotId = plotId;
        _yaw = camera.Look?.Yaw ?? 0f;
        _pitch = StartPitch;
        _pan = Vector2.Zero;
        if (plotId != 0 && building.TryFindPlot(plotId, out var plot))
        {
            _focus = plot.Center with { Z = plot.GroundZ };
            _distance = Math.Clamp(Math.Max(plot.MaxX - plot.MinX, plot.MaxY - plot.MinY) * PlotView, LeastDistance, MostDistance);
        }
        else if (TryFindWhereThePlayerLooks(out var looked))
        {
            _focus = looked;
            _distance = MarkingDistance;
        }
        else
        {
            logger.LogWarning("[CyrodiilRP] Architect mode found nothing the player looks at to mark a plot from, so the game's camera stays.");
            End();
            return;
        }

        _home = _focus;
        _active = true;
        _paused = false;
        Place(GlideSeconds);
    }

    public void End()
    {
        if (!_active)
            return;

        _active = false;
        _paused = false;
        _pan = Vector2.Zero;
        camera.Reset(GlideSeconds);
    }

    // While the player looks around with the game's camera, and back.
    public void Pause(bool paused)
    {
        if (!_active || _paused == paused)
            return;

        _paused = paused;
        _pan = Vector2.Zero;
        if (paused)
            camera.Reset(GlideSeconds);
        else
            Place(GlideSeconds);
    }

    // The WASD held: forward (1) or back (-1), right (1) or left (-1), 0 for none.
    public void Pan(int forward, int right) => _pan = new Vector2(Math.Sign(forward), Math.Sign(right));

    // The right mouse button dragged, in pixels: across turns round the point, up and down tilts.
    public void Orbit(float across, float down)
    {
        if (!IsActive)
            return;

        _yaw = (_yaw + across * DegreesPerPixel) % 360f;
        _pitch = Math.Clamp(_pitch - down * DegreesPerPixel, LeastPitch, MostPitch);
        Place(0f);
    }

    // Notches of the wheel: positive, rolled towards the player, takes the view further away.
    public void Zoom(int notches)
    {
        if (!IsActive || notches == 0)
            return;

        _distance = Math.Clamp(_distance * MathF.Pow(ZoomStep, notches), LeastDistance, MostDistance);
        Place(0f);
    }

    private void Update()
    {
        if (!IsActive)
        {
            _lastTick = 0;
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var seconds = _lastTick == 0 ? 0f : (float)Math.Min(Stopwatch.GetElapsedTime(_lastTick, now).TotalSeconds, MostFrameSeconds);
        _lastTick = now;
        if (_pan != Vector2.Zero && seconds > 0f)
        {
            var look = new CameraLook(0f, _yaw);
            var by = (look.Ahead * _pan.X + look.Right * _pan.Y) * (_distance * PanPerSecond * seconds);
            _focus = Hold(_focus + by);
            Place(0f);
            return;
        }

        // A load took the camera away
        if (!camera.IsSet)
            Place(0f);
    }

    private void Place(float glideSeconds)
    {
        var position = _focus - new CameraLook(_pitch, _yaw).Forward * _distance;
        if (camera.Set(position, _focus, glideSeconds))
            Moved?.Invoke();
    }

    private Vector3 Hold(Vector3 focus)
    {
        if (_plotId != 0 && building.TryFindPlot(_plotId, out var plot))
        {
            return focus with
            {
                X = Math.Clamp(focus.X, plot.MinX - PlotMargin, plot.MaxX + PlotMargin),
                Y = Math.Clamp(focus.Y, plot.MinY - PlotMargin, plot.MaxY + PlotMargin),
            };
        }

        var away = new Vector2(focus.X - _home.X, focus.Y - _home.Y);
        if (away.Length() <= MarkingReach)
            return focus;

        var held = Vector2.Normalize(away) * MarkingReach;
        return focus with { X = _home.X + held.X, Y = _home.Y + held.Y };
    }

    private bool TryFindWhereThePlayerLooks(out Vector3 point)
    {
        point = default;
        if (!props.TryTraceScreen(0.5f, 0.5f, Reach, out var view))
            return false;

        point = view.Hit == ScreenHit.Nothing ? view.Origin + view.Direction * FallbackAhead : view.Location;
        return true;
    }
}
