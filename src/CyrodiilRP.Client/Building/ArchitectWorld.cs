using System.Numerics;
using CyrodiilRP.Common.Building;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Props;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Building;

// What architect mode shows in the world: the plot's outline, the piece the mouse carries (its mesh without collision and
// its box, green where it fits and red where not), the piece picked and the plot being marked. The point under the mouse
// comes from the line the game's camera casts through it (ILocalProps.TryTraceScreen). The server checks every change
// again. Update thread only.
[Service]
internal sealed partial class ArchitectWorld(ILocalProps props, ICamera camera, BuildingProps building, ArchitectCamera view, RpcHandlers rpc,
    ILogger logger)
{
    // How far the mouse reaches into the world, in centimetres.
    private const float Reach = 20_000f;

    // How high the outline of a plot being marked is drawn, in centimetres. The form sets the real height.
    private const float MarkHeight = 600f;

    // A plot reaches down without end: its outline is drawn this far under its ground, in centimetres, so it shows below
    // a dip in the land.
    private const float ShownBelow = 5000f;

    // The sign's footing is looked for from this far over the plot's ground, and as far under it.
    private const float SignSearch = 3000f;

    private const float LineThickness = 4f;
    private const float PlotLineThickness = 8f;

    // A plot's sides, shaded so it shows from afar: how much of the green shows, and how thick a side is, in centimetres.
    private const float PlotSideOpacity = 0.4f;
    private const float PlotSideHalf = 1f;

    // The gizmo over the picked piece: how high over its top, its arrows' length as a share of the view's distance and
    // within limits, how thick, a head's half size, how solid, and how near the mouse a handle is grabbed, in shares of
    // the screen.
    private const float GizmoLift = 30f;
    private const float GizmoShare = 0.12f;
    private const float LeastGizmo = 80f;
    private const float MostGizmo = 600f;
    private const float GizmoHalf = 3f;
    private const float GizmoHead = 10f;
    private const float GizmoOpacity = 1f;
    private const float GizmoReach = 0.02f;
    private const float GizmoThickness = 2f;
    private const float FreeView = 2000f;

    private const float SignFacing = 180f;

    // The game's own houses are built of forms under this path, whose parts teach how each kit mesh stands upright.
    private const string FormsPath = "/Game/Forms/";

    private const float DegreesPerRadian = 180f / MathF.PI;

    // How far an arrow moves the picked piece with the grid off, in centimetres.
    private const float FreeNudge = 10f;

    // The most parts a piece is taken apart into (ArchitectRules.MostParts on the server), and a part's numbers: x, y, z,
    // pitch, yaw and roll.
    private const int MostParts = 200;
    private const int NumbersPerPart = 6;

    // A part's scale this near 1 is whole.
    private const float ScaleSlack = 0.01f;

    // How near the server's spot is to the one sent for the two to be one, in centimetres and degrees.
    private const float SameSpot = 1f;
    private const float SameTurn = 0.01f;

    // How far above the mouse's hit a carried piece is let fall from, and how far at most, in centimetres.
    private const float RestLift = 60f;
    private const float RestDrop = 2000f;

    private static readonly int[] Steps = [1, 2, 4, 0];

    private readonly Dictionary<string, CataloguePiece> _catalogue = new(StringComparer.Ordinal);

    private long _plotId;
    private ArchitectTool _tool;
    private bool _marking;
    private ArchitectDrag _drag;
    private string? _carried;
    private long _moving;
    private float _yaw;
    private float _raise;
    // The carried piece's pitch and roll: what stands a kit's mesh upright, or the turn a piece already had.
    private Vector2 _tilt;

    // How each kit mesh stands upright, its pitch and roll, by its asset: as the catalogue's houses stand it. Learned
    // once, when a mesh is first carried.
    private readonly Dictionary<string, Vector2> _upright = new(StringComparer.OrdinalIgnoreCase);
    private bool _uprightLearned;
    private int _stepIndex;
    private long _selected;
    // The piece picked by a click, carried only once the mouse moves with its button down.
    private PendingMove? _pending;
    // Where the mouse last was over the world, so what it carries follows when the view moves under it.
    private Vector2? _lastPoint;
    private LocalPropHandle? _highlighted;
    private Vector3? _aimed;
    private Vector3? _spot;
    private GhostFit _fit;
    private Vector3 _markStart;
    private PlotBox? _marked;

    private LocalPropHandle? _ghost;
    private LocalPropHandle? _ghostBox;
    private LocalPropHandle? _selectionBox;
    private readonly List<PlotShape> _plotShape = [];
    private readonly List<PlotShape> _choiceBoxes = [];
    private readonly List<PlotShape> _markShape = [];
    private readonly List<LocalPropHandle> _gizmo = [];

    // Where the gizmo stands and how long its arrows are, while the picked piece shows one.
    private Vector3 _gizmoOrigin;
    private float _gizmoLength;

    // An axis drag: the axis, where the piece stood, where the mouse went down, and how far a centimetre along the axis
    // moves on the screen.
    private Vector3 _axis;
    private Vector3 _axisFrom;
    private Vector2 _axisMouse;
    private Vector2 _axisScreen;

    // Where the picked piece was last sent, until the server stands it there: held keys move it on from there.
    private PieceSpot? _expected;
    // What Ctrl+C copied, for Ctrl+V.
    private CopiedPiece? _copied;
    // A piece sent to be placed, picked once the server stands it.
    private PieceSpot? _pickWhenPlaced;
    private string? _pickAsset;
    private long _pickAbove;

    // The plot being marked: the side its front and sign are at, and whether the staff turned it (or it faces the view).
    private PlotEdge _markFront;
    private bool _frontTurned;
    private LocalPropHandle? _signGhost;

    public event Action<GhostFit>? FitChanged;

    public event Action<string>? SelectionChanged;

    public event Action<int, int>? MarkChanged;

    // A turn or a lift of the picked piece that would not fit.
    public event Action<GhostFit>? Refused;

    // Why the picked piece could not be taken apart.
    public event Action<BreakRefusal>? BreakRefused;

    // Steps of the grid a tile: 1, 2 or 4, or 0 for none.
    public int Step => Steps[_stepIndex];

    // A carried piece settles on what is under it, or stands on the plot's ground.
    public bool Settle { get; set; } = true;

    // How far a step up or down goes, in centimetres, as the page's panel picks it: the gizmo's height snaps to it.
    public int HeightStep { get; set; } = 10;

    public void SetCatalogue(string[] keys, string[] assets, float[] scales)
    {
        if (assets.Length != keys.Length || scales.Length != keys.Length)
        {
            logger.LogError("[CyrodiilRP] The server sent {Keys} keys, {Assets} assets and {Scales} scales for the building catalogue.",
                keys.Length, assets.Length, scales.Length);
            return;
        }

        for (var index = 0; index < keys.Length; index++)
        {
            var old = _catalogue.GetValueOrDefault(keys[index]);
            _catalogue[keys[index]] = new CataloguePiece(keys[index], assets[index], scales[index],
                old is not null && old.Asset == assets[index] && old.Scale == scales[index] ? old.Bounds : null);
        }
    }

    // On the plot, or (0) choosing one or marking a new one.
    public void Begin(long plotId, ArchitectTool tool)
    {
        Cancel();
        Deselect();
        ClearMark();
        _plotId = plotId;
        _tool = tool;
        _marking = tool == ArchitectTool.Mark;
        DrawPlot();
    }

    public void End()
    {
        Cancel();
        Deselect();
        ClearMark();
        RemoveShapes(_plotShape);
        RemoveChoices();
        _plotId = 0;
        _tool = ArchitectTool.Choose;
        _marking = false;
    }

    // Again after the server's plots came, so a plot whose size changed is drawn as it is. Choosing, every plot around.
    public void DrawPlot()
    {
        RemoveShapes(_plotShape);
        RemoveChoices();
        switch (_tool)
        {
            case ArchitectTool.Choose:
                foreach (var id in building.PlotIds)
                {
                    if (building.TryFindPlot(id, out var each))
                        DrawPlotShape(each, _choiceBoxes);
                }

                return;
            case ArchitectTool.Mark:
                return;
            case ArchitectTool.Build:
                if (building.TryFindPlot(_plotId, out var box))
                    DrawPlotShape(box, _plotShape);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(_tool), _tool, "Not an architect tool.");
        }
    }

    private void RemoveChoices() => RemoveShapes(_choiceBoxes);

    private void RemoveAll(List<LocalPropHandle> handles)
    {
        foreach (var handle in handles)
            props.Remove(handle);
        handles.Clear();
    }

    // A plot's green outline and its sides shaded green, so a plot is found at a glance.
    private void DrawPlotShape(PlotBox box, List<PlotShape> into)
    {
        var shape = new PlotShape(box, props.PlaceBox(PlotOutline(box)), [], NoSides);
        into.Add(ShadeSides(shape));
    }

    private void RemoveShapes(List<PlotShape> shapes)
    {
        foreach (var shape in shapes)
        {
            props.Remove(shape.Outline);
            foreach (var side in shape.Sides)
                props.Remove(side);
        }

        shapes.Clear();
    }

    // Only the sides across the plot from the view are shaded, as if each showed only its inner face: the near ones would
    // hide what is built behind them. From over the plot every side is across it.
    private PlotShape ShadeSides(PlotShape shape)
    {
        var shown = view.IsActive ? SidesSeenFrom(shape.Box, view.Position) : AllSides;
        if (shown == shape.Shown)
            return shape;

        foreach (var side in shape.Sides)
            props.Remove(side);

        var outline = PlotOutline(shape.Box);
        var center = outline.Center;
        var half = outline.HalfSize;
        var sides = new List<LocalPropHandle>();
        if ((shown & LowXSide) != 0)
            sides.Add(Side(center with { X = shape.Box.MinX }, half with { X = PlotSideHalf }));
        if ((shown & HighXSide) != 0)
            sides.Add(Side(center with { X = shape.Box.MaxX }, half with { X = PlotSideHalf }));
        if ((shown & LowYSide) != 0)
            sides.Add(Side(center with { Y = shape.Box.MinY }, half with { Y = PlotSideHalf }));
        if ((shown & HighYSide) != 0)
            sides.Add(Side(center with { Y = shape.Box.MaxY }, half with { Y = PlotSideHalf }));
        return shape with { Sides = sides, Shown = shown };

        LocalPropHandle Side(Vector3 at, Vector3 size) =>
            props.PlaceFill(new BoxFill(at, size, 0f, Colour.Plot.Red, Colour.Plot.Green, Colour.Plot.Blue, PlotSideOpacity));
    }

    // A side is across the plot when the view stands on the plot's side of it.
    private static int SidesSeenFrom(PlotBox box, Vector3 from) =>
        (from.X > box.MinX ? LowXSide : NoSides) | (from.X < box.MaxX ? HighXSide : NoSides)
        | (from.Y > box.MinY ? LowYSide : NoSides) | (from.Y < box.MaxY ? HighYSide : NoSides);

    private void ShadeAll(List<PlotShape> shapes)
    {
        for (var index = 0; index < shapes.Count; index++)
            shapes[index] = ShadeSides(shapes[index]);
    }

    // A plot's sides, as bits of which are shaded.
    private const int NoSides = 0;
    private const int LowXSide = 1;
    private const int HighXSide = 2;
    private const int LowYSide = 4;
    private const int HighYSide = 8;
    private const int AllSides = LowXSide | HighXSide | LowYSide | HighYSide;

    // A plot as drawn: its outline, the side fills shown, and which sides those are.
    private sealed record PlotShape(PlotBox Box, LocalPropHandle Outline, IReadOnlyList<LocalPropHandle> Sides, int Shown);

    // The mouse went down: on a drawer's piece (its key), or on the world (empty).
    public void Grab(float x, float y, string piece)
    {
        Cancel();
        if (piece.Length > 0)
        {
            if (_tool != ArchitectTool.Build || !_catalogue.TryGetValue(piece, out var carried))
            {
                logger.LogWarning("[CyrodiilRP] The architect page carried '{Piece}', which the catalogue does not list here.", piece);
                return;
            }

            Measure(carried);
            _drag = ArchitectDrag.NewPiece;
            _carried = piece;
            _yaw = 0f;
            _tilt = UprightOf(carried.Asset);
            _raise = 0f;
            Point(x, y);
            return;
        }

        if (!props.TryTraceScreen(x, y, Reach, out var trace) || trace.Hit == ScreenHit.Nothing)
            return;

        if (_tool == ArchitectTool.Choose)
        {
            ChoosePlotAt(trace.Location);
            return;
        }

        if (TryPickAxis(x, y, out var axis))
        {
            StartAxisDrag(axis, x, y);
            return;
        }

        if (_marking)
        {
            _drag = ArchitectDrag.MarkPlot;
            _markStart = trace.Location;
            ClearMark();
            Point(x, y);
            return;
        }

        var picked = trace.Hit switch
        {
            ScreenHit.LocalProp => building.TryFindPiece(trace.Prop, out var mesh) ? mesh : 0,
            ScreenHit.Reference => building.TryFindByReference(trace.RefId, out var reference) ? reference : 0,
            ScreenHit.World or ScreenHit.Nothing => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(trace), trace.Hit, "Not a screen hit."),
        };
        if (picked != 0 && building.TryFindPiece(picked, out var prop) && FindKey(prop.Asset) is { } key)
        {
            Select(picked, key);
            _pending = new PendingMove(picked, key, prop.Rotation.Y, new Vector2(prop.Rotation.X, prop.Rotation.Z));
            return;
        }

        Deselect();
    }

    // A click on a plot's ground opens it to build on.
    private void ChoosePlotAt(Vector3 point)
    {
        foreach (var id in building.PlotIds)
        {
            if (building.TryFindPlot(id, out var box) && box.Contains(point.X, point.Y))
            {
                rpc.SendArchitectChoose(id);
                return;
            }
        }
    }

    // The picked piece is carried from where it stands once the mouse moved with its button down.
    private void StartCarrying(PendingMove pending)
    {
        _pending = null;
        Unhighlight();
        Remove(ref _selectionBox);
        RemoveAll(_gizmo);
        _drag = ArchitectDrag.MovePiece;
        _carried = pending.Key;
        _moving = pending.Id;
        _yaw = pending.Yaw;
        _tilt = pending.Tilt;
        _raise = 0f;
        building.Hide(pending.Id);
    }

    public void Point(float x, float y)
    {
        if (_drag == ArchitectDrag.None && _pending is { } pending)
            StartCarrying(pending);

        _lastPoint = new Vector2(x, y);

        switch (_drag)
        {
            case ArchitectDrag.None:
                return;
            case ArchitectDrag.NewPiece:
            case ArchitectDrag.MovePiece:
                AimPiece(x, y);
                return;
            case ArchitectDrag.MarkPlot:
                AimMark(x, y);
                return;
            case ArchitectDrag.AxisMove:
                AimAxis(x, y);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(_drag), _drag, "Not an architect drag.");
        }
    }

    // The gizmo's handle under the mouse, if any: the arrow whose line on the screen passes nearest.
    private bool TryPickAxis(float x, float y, out Vector3 axis)
    {
        axis = default;
        if (_gizmo.Count == 0)
            return false;

        var mouse = new Vector2(x, y);
        var nearest = GizmoReach;
        foreach (var each in Axes)
        {
            if (!camera.TryProject(_gizmoOrigin, out var from) || !camera.TryProject(_gizmoOrigin + each * _gizmoLength, out var to))
                continue;

            var distance = DistanceToSegment(mouse, from, to);
            if (distance > nearest)
                continue;

            nearest = distance;
            axis = each;
        }

        return axis != default;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var along = to - from;
        var share = along.LengthSquared() > 0f ? Math.Clamp(Vector2.Dot(point - from, along) / along.LengthSquared(), 0f, 1f) : 0f;
        return Vector2.Distance(point, from + along * share);
    }

    // The picked piece carried along one axis: the rest of it stays where it stood.
    private void StartAxisDrag(Vector3 axis, float x, float y)
    {
        if (!TryFindSelected(out var prop) || FindKey(prop.Asset) is not { } key
            || !camera.TryProject(_gizmoOrigin, out var from) || !camera.TryProject(_gizmoOrigin + axis * _gizmoLength, out var to))
            return;

        var screen = (to - from) / _gizmoLength;
        if (screen.LengthSquared() <= float.Epsilon)
            return;

        _pending = null;
        RemoveAll(_gizmo);
        Unhighlight();
        Remove(ref _selectionBox);
        _drag = ArchitectDrag.AxisMove;
        _carried = key;
        _moving = _selected;
        _yaw = prop.Rotation.Y;
        _tilt = new Vector2(prop.Rotation.X, prop.Rotation.Z);
        _raise = 0f;
        _axis = axis;
        _axisFrom = prop.Position;
        _axisMouse = new Vector2(x, y);
        _axisScreen = screen;
        building.Hide(_moving);
        ShowGhost(prop.Position);
    }

    // Along the axis as far as the mouse went along its line on the screen, snapped as the grid or the height step say.
    private void AimAxis(float x, float y)
    {
        var moved = new Vector2(x, y) - _axisMouse;
        var centimetres = Vector2.Dot(moved, _axisScreen) / _axisScreen.LengthSquared();
        var at = _axisFrom + _axis * centimetres;
        if (_axis.X != 0f && Step > 0)
            at.X = BuildingGrid.Snap(at.X, Step);
        if (_axis.Y != 0f && Step > 0)
            at.Y = BuildingGrid.Snap(at.Y, Step);
        if (_axis.Z != 0f && HeightStep > 0)
        {
            var ground = building.TryFindPlot(_plotId, out var plot) ? plot.GroundZ : 0f;
            at.Z = ground + MathF.Round((at.Z - ground) / HeightStep) * HeightStep;
        }

        ShowGhost(at);
    }

    // Arrows along the world's axes from over the picked piece, red, green and blue, each with a head to grab.
    private void DrawGizmo(Prop prop, PropBounds bounds)
    {
        RemoveAll(_gizmo);
        var top = Vector3.Transform(bounds.Center, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, prop.Rotation.Y * MathF.PI / 180f));
        _gizmoOrigin = prop.Position + new Vector3(top.X, top.Y, bounds.Max.Z + GizmoLift);
        _gizmoLength = Math.Clamp((view.IsActive ? view.Distance : FreeView) * GizmoShare, LeastGizmo, MostGizmo);
        foreach (var axis in Axes)
        {
            var colour = axis.X != 0f ? Colour.AxisX : axis.Y != 0f ? Colour.AxisY : Colour.AxisZ;
            var shaft = new Vector3(GizmoHalf) + axis * (_gizmoLength / 2f - GizmoHalf);
            var middle = _gizmoOrigin + axis * (_gizmoLength / 2f);
            _gizmo.Add(props.PlaceFill(new BoxFill(middle, shaft, 0f, colour.Red, colour.Green, colour.Blue, GizmoOpacity)));
            _gizmo.Add(props.PlaceBox(Outline(middle, shaft, 0f, colour) with { Thickness = GizmoThickness }));
            _gizmo.Add(props.PlaceFill(new BoxFill(_gizmoOrigin + axis * _gizmoLength, new Vector3(GizmoHead), 0f, colour.Red, colour.Green,
                colour.Blue, GizmoOpacity)));
        }
    }

    private static readonly Vector3[] Axes = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];

    // The mouse went up over the world: what it carried goes there, if it fits.
    public void Drop(float x, float y)
    {
        // A click on a piece picks it and leaves it where it stands
        if (_drag == ArchitectDrag.None)
        {
            _pending = null;
            return;
        }

        Point(x, y);
        switch (_drag)
        {
            case ArchitectDrag.None:
                return;
            case ArchitectDrag.NewPiece:
                if (_fit == GhostFit.Fits && _spot is { } spot && _carried is { } piece)
                    Place(piece, spot, CarriedRotation);
                Cancel();
                return;
            case ArchitectDrag.MovePiece:
            case ArchitectDrag.AxisMove:
                if (_fit == GhostFit.Fits && _spot is { } moved)
                    rpc.SendArchitectMove(_moving, [moved.X, moved.Y, moved.Z, _yaw]);
                Cancel();
                return;
            case ArchitectDrag.MarkPlot:
                _drag = ArchitectDrag.None;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(_drag), _drag, "Not an architect drag.");
        }
    }

    // What the mouse carried goes back where it was, a piece out of the drawer back into it.
    public void Cancel()
    {
        if (_drag is ArchitectDrag.MovePiece or ArchitectDrag.AxisMove)
            building.Show(_moving);

        _drag = ArchitectDrag.None;
        _pending = null;
        _lastPoint = null;
        _carried = null;
        _moving = 0;
        _spot = null;
        Remove(ref _ghost);
        _aimed = null;
        Remove(ref _ghostBox);
        SetFit(GhostFit.None);
        if (_selected != 0)
            DrawSelection();
    }

    // The carried piece, or else the picked one, which is moved at once if it still fits.
    public void Turn(float degrees)
    {
        if (_drag is ArchitectDrag.NewPiece or ArchitectDrag.MovePiece)
        {
            _yaw = BuildingGrid.StepToward(_yaw, 0f, degrees);
            Redraw();
            return;
        }

        if (TryFindSelected(out var prop))
            MoveSelected(prop, prop.Position, BuildingGrid.StepToward(prop.Rotation.Y, 0f, degrees));
    }

    // Up or down to the next step of the size asked: from where the piece rests while it is carried, from the plot's
    // ground once it stands, so pieces raised alike stand level.
    public void Raise(float centimetres)
    {
        if (_drag is ArchitectDrag.NewPiece or ArchitectDrag.MovePiece)
        {
            _raise = BuildingGrid.StepToward(_raise, 0f, centimetres);
            Redraw();
            return;
        }

        if (!TryFindSelected(out var prop))
            return;

        var ground = building.TryFindPlot(_plotId, out var plot) ? plot.GroundZ : 0f;
        var z = BuildingGrid.StepToward(prop.Position.Z, ground, centimetres);
        MoveSelected(prop, prop.Position with { Z = z }, prop.Rotation.Y);
    }

    public int NextStep()
    {
        _stepIndex = (_stepIndex + 1) % Steps.Length;
        return Step;
    }

    // One of 1, 2, 4 or 0, as the page's panel picks it. Another number leaves the grid as it is.
    public int SetStep(int steps)
    {
        var index = Array.IndexOf(Steps, steps);
        if (index < 0)
            logger.LogWarning("[CyrodiilRP] The architect page asked for {Steps} steps a tile, which is no grid.", steps);
        else
            _stepIndex = index;
        return Step;
    }

    public void RemoveSelected()
    {
        if (_selected == 0)
            return;

        rpc.SendArchitectRemove(_selected);
        Deselect();
    }

    // The picked piece to the next grid line along the ground: forward is the axis of the grid nearest to where the camera
    // looks, so the arrows move it as they read on screen.
    public void Nudge(int forward, int right)
    {
        if (_drag != ArchitectDrag.None || !TryFindSelected(out var prop) || !TryFindGridAxes(out var ahead, out var aside))
            return;

        var length = Step > 0 ? BuildingGrid.TileCentimetres / Step : FreeNudge;
        var by = (ahead * forward + aside * right) * length;
        var to = new Vector3(BuildingGrid.StepToward(prop.Position.X, 0f, by.X), BuildingGrid.StepToward(prop.Position.Y, 0f, by.Y), prop.Position.Z);
        MoveSelected(prop, to, prop.Rotation.Y);
    }

    // The grid's axis nearest to where the camera looks, and the one to its right.
    private bool TryFindGridAxes(out Vector2 ahead, out Vector2 aside)
    {
        ahead = default;
        aside = default;
        var looking = view.Ahead;
        if (!view.IsActive)
        {
            if (!props.TryTraceScreen(0.5f, 0.5f, Reach, out var trace))
                return false;

            looking = trace.Direction;
        }

        ahead = MathF.Abs(looking.X) >= MathF.Abs(looking.Y)
            ? new Vector2(MathF.Sign(looking.X), 0f)
            : new Vector2(0f, MathF.Sign(looking.Y));
        // Unreal's yaw turns from +X towards +Y, so the right hand of +X is +Y
        aside = new Vector2(-ahead.Y, ahead.X);
        return true;
    }

    // The picked piece's key and turn, for Paste.
    public bool Copy()
    {
        if (!TryFindSelected(out var prop) || FindKey(prop.Asset) is not { } key)
            return false;

        _copied = new CopiedPiece(key, prop.Rotation);
        return true;
    }

    // What Copy took, put down under the mouse as a piece out of the drawer is, if it fits there.
    public void Paste(float x, float y)
    {
        if (_tool != ArchitectTool.Build || _copied is not { } copied || !_catalogue.TryGetValue(copied.Key, out var piece))
            return;

        Cancel();
        Measure(piece);
        _drag = ArchitectDrag.NewPiece;
        _carried = copied.Key;
        _yaw = copied.Rotation.Y;
        _tilt = new Vector2(copied.Rotation.X, copied.Rotation.Z);
        _raise = 0f;
        Point(x, y);
        if (_fit == GhostFit.Fits && _spot is { } spot)
            Place(copied.Key, spot, CarriedRotation);
        else
            Refused?.Invoke(_fit == GhostFit.None ? GhostFit.NoGround : _fit);

        Cancel();
    }

    // A copy of the picked piece where it stands, picked once it stands, to be moved off it with the keys: in place it
    // fits wherever the piece does.
    public void Duplicate()
    {
        if (_drag != ArchitectDrag.None || !TryFindSelected(out var prop) || FindKey(prop.Asset) is not { } key)
            return;

        Place(key, prop.Position, prop.Rotation);
    }

    // The picked piece let fall onto what is under it, or onto the plot's ground, as a carried piece settles. The game's
    // own furniture stands in its own way, so it cannot be let fall.
    public void SnapToFloor()
    {
        if (_drag != ArchitectDrag.None || !TryFindSelected(out var prop) || FindKey(prop.Asset) is not { } key
            || !_catalogue.TryGetValue(key, out var piece))
            return;

        Measure(piece);
        piece = _catalogue[key];
        var bottom = piece.Bounds is { } bounds ? bounds.Min.Z : 0f;
        float? z;
        if (!Settle && building.TryFindPlot(_plotId, out var plot))
        {
            z = plot.GroundZ - bottom;
        }
        else if (!building.TryFindHandle(_selected, out _))
        {
            z = null;
        }
        else
        {
            // Taken down for the fall, so it does not land on itself
            var height = piece.Bounds is { } box ? box.Size.Z : 0f;
            building.Hide(_selected);
            z = FindRest(piece, prop.Position.X, prop.Position.Y, prop.Position.Z + height, prop.Rotation.Y);
            building.Show(_selected);
            DrawSelection();
        }

        if (z is not { } rest)
        {
            Refused?.Invoke(GhostFit.NoGround);
            return;
        }

        MoveSelected(prop, prop.Position with { Z = rest }, prop.Rotation.Y);
    }

    // The picked piece taken apart into the meshes it is built of (ILocalProps.TryListParts), each a piece of a kit of the
    // catalogue standing where it stood in it. The server takes the piece away and stands the parts in one write.
    public void BreakApart()
    {
        if (_drag != ArchitectDrag.None || !TryFindSelected(out var prop))
            return;

        if (!building.TryFindHandle(_selected, out _))
        {
            BreakRefused?.Invoke(BreakRefusal.GameObject);
            return;
        }

        if (!props.TryListParts(prop.Asset, out var parts) || parts.Count == 0)
        {
            BreakRefused?.Invoke(BreakRefusal.NoParts);
            return;
        }

        if (parts.Count == 1)
        {
            BreakRefused?.Invoke(BreakRefusal.OnePart);
            return;
        }

        if (parts.Count > MostParts)
        {
            BreakRefused?.Invoke(BreakRefusal.TooMany);
            return;
        }

        // The game names a mesh in its own letter case, which need not be the catalogue's
        var byAsset = _catalogue.Values.GroupBy(piece => piece.Asset, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, prop.Rotation.Y * MathF.PI / 180f);
        var keys = new List<string>(parts.Count);
        var spots = new List<float>(parts.Count * NumbersPerPart);
        foreach (var part in parts)
        {
            if (!IsWhole(part.Scale) || MathF.Abs(prop.Scale - 1f) > ScaleSlack)
            {
                BreakRefused?.Invoke(BreakRefusal.Stretched);
                return;
            }

            if (!byAsset.TryGetValue(part.Mesh, out var piece))
            {
                logger.LogWarning("[CyrodiilRP] {Asset} cannot be taken apart: its part {Mesh} is in no kit of the catalogue.", prop.Asset, part.Mesh);
                BreakRefused?.Invoke(BreakRefusal.NotInCatalogue);
                return;
            }

            Measure(piece);
            var at = prop.Position + Vector3.Transform(part.Position, turn);
            keys.Add(piece.Key);
            spots.AddRange([at.X, at.Y, at.Z, part.Rotation.X, part.Rotation.Y + prop.Rotation.Y, part.Rotation.Z]);
        }

        rpc.SendArchitectBreakApart(_selected, keys.ToArray(), spots.ToArray());
        Deselect();
    }

    private static bool IsWhole(Vector3 scale) =>
        MathF.Abs(scale.X - 1f) <= ScaleSlack && MathF.Abs(scale.Y - 1f) <= ScaleSlack && MathF.Abs(scale.Z - 1f) <= ScaleSlack;

    // The server refused the last change: the picked piece is where the server last stood it.
    public void ForgetExpected() => _expected = null;

    // Rotation: pitch, yaw and roll.
    private void Place(string key, Vector3 spot, Vector3 rotation)
    {
        rpc.SendArchitectPlace(key, [spot.X, spot.Y, spot.Z, rotation.Y, rotation.X, rotation.Z]);
        if (_catalogue.TryGetValue(key, out var piece))
        {
            _pickWhenPlaced = new PieceSpot(spot, rotation.Y);
            _pickAsset = piece.Asset;
            _pickAbove = building.HighestPieceId;
        }
    }

    // The picked piece where it was last sent, until the server stands it there.
    private bool TryFindSelected(out Prop prop)
    {
        if (_selected == 0 || !building.TryFindPiece(_selected, out prop))
        {
            prop = default;
            return false;
        }

        if (_expected is { } expected)
            prop = prop with { Position = expected.Position, Rotation = prop.Rotation with { Y = expected.Yaw } };
        return true;
    }

    public void Deselect()
    {
        _selected = 0;
        _expected = null;
        _pending = null;
        Unhighlight();
        Remove(ref _selectionBox);
        RemoveAll(_gizmo);
        SelectionChanged?.Invoke(string.Empty);
    }

    // The marked plot with the page's form. False with nothing marked.
    public bool SaveMarked(string form)
    {
        if (!_marking || _marked is not { } box)
            return false;

        var sign = SignSpot(box, _markFront);
        rpc.SendArchitectMarkPlot([box.FirstTileX, box.FirstTileY, box.Width, box.Depth],
            [box.GroundZ, sign.X, sign.Y, sign.Z, SignYaw(_markFront)], form);
        return true;
    }

    public void ClearMark()
    {
        _marked = null;
        _frontTurned = false;
        Remove(ref _signGhost);
        RemoveShapes(_markShape);
        MarkChanged?.Invoke(0, 0);
    }

    // The game measures a mesh the first time it is carried, and the server keeps the first measurement it gets.
    private void Measure(CataloguePiece piece)
    {
        if (piece.Bounds is not null || !props.TryMeasure(piece.Asset, piece.Scale, out var bounds))
            return;

        // A kit mesh stands tilted upright: its box is the one it fills standing so, which its yaw then turns
        var tilt = UprightOf(piece.Asset);
        if (tilt != Vector2.Zero)
            bounds = Tilted(bounds, new Vector3(tilt.X, 0f, tilt.Y));

        _catalogue[piece.Key] = piece with { Bounds = bounds };
        rpc.SendArchitectMeasured(piece.Key, [bounds.Min.X, bounds.Min.Y, bounds.Min.Z, bounds.Max.X, bounds.Max.Y, bounds.Max.Z]);
    }

    private void AimPiece(float x, float y)
    {
        if (!props.TryTraceScreen(x, y, Reach, out var trace) || trace.Hit == ScreenHit.Nothing)
        {
            _aimed = null;
            ShowGhost(null);
            return;
        }

        _aimed = trace.Location;
        Redraw();
    }

    // At the last point aimed at, with the turn and the height chosen since.
    private void Redraw()
    {
        if (_aimed is not { } at || _carried is null || !_catalogue.TryGetValue(_carried, out var piece))
        {
            ShowGhost(null);
            return;
        }

        var x = BuildingGrid.Snap(at.X, Step);
        var y = BuildingGrid.Snap(at.Y, Step);
        var bottom = piece.Bounds is { } bounds ? bounds.Min.Z : 0f;
        var z = !Settle && building.TryFindPlot(_plotId, out var plot)
            ? plot.GroundZ - bottom
            : FindRest(piece, x, y, at.Z - bottom, _yaw) ?? at.Z - bottom;
        ShowGhost(new Vector3(x, y, z + _raise));
    }

    // The height the piece's box comes to rest at under the snapped spot, dropped from a little above where the mouse's
    // line hit: on the ground or on the highest thing under its whole footprint, not on whatever the line grazed. Null
    // when it starts inside something or has nothing under it, and the hit stands.
    private float? FindRest(CataloguePiece piece, float x, float y, float hitZ, float yaw)
    {
        var start = new Vector3(x, y, hitZ + RestLift);
        var prop = new Prop(piece.Asset, start, new Vector3(0f, yaw, 0f), piece.Scale);
        return props.FindRest(prop, start, RestDrop, out var rest) == PropRest.Rests ? rest.Z : null;
    }

    private void ShowGhost(Vector3? spot)
    {
        _spot = spot;
        if (spot is not { } position || _carried is null || !_catalogue.TryGetValue(_carried, out var piece))
        {
            Remove(ref _ghost);
            Remove(ref _ghostBox);
            SetFit(_drag == ArchitectDrag.None ? GhostFit.None : GhostFit.NoGround);
            return;
        }

        var prop = new Prop(piece.Asset, position, CarriedRotation, piece.Scale);
        if (_ghost is { } ghost)
            props.Move(ghost, prop.Position, prop.Rotation);
        else
            _ghost = props.Place(prop, collides: false);

        var fit = Judge(piece, position, _yaw);
        SetFit(fit);
        Remove(ref _ghostBox);
        if (piece.Bounds is { } bounds)
            _ghostBox = props.PlaceBox(PieceOutline(bounds, position, _yaw, fit == GhostFit.Fits ? Colour.Fits : Colour.Refused));
    }

    private GhostFit Judge(CataloguePiece piece, Vector3 position, float yaw)
    {
        if (!building.TryFindPlot(_plotId, out var box))
            return GhostFit.Outside;

        var fit = piece.Bounds is { } bounds
            ? box.Judge(bounds.Min, bounds.Max, position, yaw)
            : box.Contains(position.X, position.Y) ? position.Z > box.Top ? PlotFit.TooHigh : PlotFit.Fits : PlotFit.Outside;
        return fit switch
        {
            PlotFit.Fits => GhostFit.Fits,
            PlotFit.Outside => GhostFit.Outside,
            PlotFit.TooHigh => GhostFit.TooHigh,
        };
    }

    private void AimMark(float x, float y)
    {
        if (!props.TryTraceScreen(x, y, Reach, out var trace) || trace.Hit == ScreenHit.Nothing)
            return;

        // The land is seldom level: the plot's ground is halfway between where the drag began and where it is
        var box = PlotBox.Between(_markStart, trace.Location, (_markStart.Z + trace.Location.Z) / 2f, MarkHeight);
        _marked = box;
        if (!_frontTurned)
            _markFront = PlotSigns.Facing(box, new Vector2(trace.Origin.X, trace.Origin.Y));
        RemoveShapes(_markShape);
        DrawPlotShape(box, _markShape);
        DrawSignGhost();
        MarkChanged?.Invoke(box.Width, box.Depth);
    }

    // From under the ground (it reaches down without end) to its top.
    private static BoxOutline PlotOutline(PlotBox box)
    {
        var bottom = box.GroundZ - ShownBelow;
        var center = new Vector3((box.MinX + box.MaxX) / 2f, (box.MinY + box.MaxY) / 2f, (bottom + box.Top) / 2f);
        var half = new Vector3((box.MaxX - box.MinX) / 2f, (box.MaxY - box.MinY) / 2f, (box.Top - bottom) / 2f);
        return Outline(center, half, 0f, Colour.Plot) with { Thickness = PlotLineThickness };
    }

    // The way the sign is turned on a side: the for-sale mesh reads along its own -X, so it is turned back from the side's
    // outward yaw to face out.
    private static float SignYaw(PlotEdge edge) => PlotSigns.YawOf(edge) + SignFacing;

    // The sign at the middle of the side, outside it, its lowest point on the land there: not on what stands on it. A
    // mesh's origin need not be its foot (a hanging sign's is its hook), so its measured bottom is what stands on the land.
    private Vector3 SignSpot(PlotBox box, PlotEdge edge)
    {
        var at = PlotSigns.SpotOn(box, edge);
        var z = props.TryFindGround(at.X, at.Y, box.GroundZ + SignSearch, SignSearch * 2f, out var ground) ? ground : box.GroundZ;
        var bottom = building.SignAsset is { } asset && props.TryMeasure(asset, 1f, out var bounds) ? bounds.Min.Z : 0f;
        return new Vector3(at.X, at.Y, z - bottom);
    }

    private void DrawSignGhost()
    {
        Remove(ref _signGhost);
        if (_marked is not { } box || building.SignAsset is not { } asset)
            return;

        var spot = SignSpot(box, _markFront);
        _signGhost = props.Place(new Prop(asset, spot, new Vector3(0f, SignYaw(_markFront), 0f), 1f), collides: false);
    }

    // R: the plot being marked, or the one built on, gets its front and sign at the next side.
    public void TurnFront()
    {
        if (_drag != ArchitectDrag.None)
            return;

        if (_marking)
        {
            if (_marked is null)
                return;

            _markFront = PlotSigns.Next(_markFront);
            _frontTurned = true;
            DrawSignGhost();
            return;
        }

        if (_tool != ArchitectTool.Build || _selected != 0 || !building.TryFindPlot(_plotId, out var plot)
            || !building.TryFindSign(_plotId, out var sign))
            return;

        var next = PlotSigns.Next(PlotSigns.EdgeOf(plot, new Vector2(sign.X, sign.Y)));
        var spot = SignSpot(plot, next);
        rpc.SendArchitectMoveSign([spot.X, spot.Y, spot.Z, SignYaw(next)]);
    }

    private void MoveSelected(Prop prop, Vector3 position, float yaw)
    {
        var key = FindKey(prop.Asset);
        if (key is null || !_catalogue.TryGetValue(key, out var piece))
            return;

        Measure(piece);
        var fit = Judge(_catalogue[key], position, yaw);
        if (fit != GhostFit.Fits)
        {
            Refused?.Invoke(fit);
            return;
        }

        rpc.SendArchitectMove(_selected, [position.X, position.Y, position.Z, yaw]);
        _expected = new PieceSpot(position, yaw);
    }

    private void Select(long id, string key)
    {
        _selected = id;
        _expected = null;
        if (_catalogue.TryGetValue(key, out var piece))
            Measure(piece);
        DrawSelection();
        SelectionChanged?.Invoke(key);
    }

    // The game's own outline on the picked mesh, and a green box round it: a piece of furniture is the game's object, which
    // only the box marks.
    private void DrawSelection()
    {
        Remove(ref _selectionBox);
        RemoveAll(_gizmo);
        Unhighlight();
        if (building.TryFindHandle(_selected, out var handle))
        {
            props.Highlight(handle, highlighted: true);
            _highlighted = handle;
        }

        if (!building.TryFindPiece(_selected, out var prop) || FindKey(prop.Asset) is not { } key
            || _catalogue.GetValueOrDefault(key)?.Bounds is not { } bounds)
            return;

        _selectionBox = props.PlaceBox(PieceOutline(bounds, prop.Position, prop.Rotation.Y, Colour.Picked));
        DrawGizmo(prop, bounds);
    }

    private void Unhighlight()
    {
        if (_highlighted is { } handle && building.HoldsHandle(handle))
            props.Highlight(handle, highlighted: false);
        _highlighted = null;
    }

    private string? FindKey(string asset) => _catalogue.Values.FirstOrDefault(p => p.Asset == asset)?.Key;

    private void SetFit(GhostFit fit)
    {
        if (fit == _fit)
            return;

        _fit = fit;
        FitChanged?.Invoke(fit);
    }

    private static BoxOutline PieceOutline(PropBounds bounds, Vector3 position, float yaw, Colour colour)
    {
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw * MathF.PI / 180f);
        return Outline(position + Vector3.Transform(bounds.Center, turn), bounds.Size / 2f, yaw, colour);
    }

    private static BoxOutline Outline(Vector3 center, Vector3 halfSize, float yaw, Colour colour) =>
        new(center, Vector3.Max(halfSize, Vector3.One), yaw, colour.Red, colour.Green, colour.Blue, LineThickness);

    private void Remove(ref LocalPropHandle? handle)
    {
        if (handle is { } placed)
            props.Remove(placed);

        handle = null;
    }

    private sealed record PendingMove(long Id, string Key, float Yaw, Vector2 Tilt);

    private sealed record PieceSpot(Vector3 Position, float Yaw);

    private sealed record CopiedPiece(string Key, Vector3 Rotation);

    private Vector3 CarriedRotation => new(_tilt.X, _yaw, _tilt.Y);

    private Vector2 UprightOf(string asset)
    {
        if (!_uprightLearned)
            LearnUpright();

        return _upright.GetValueOrDefault(asset);
    }

    // The pitch and roll each mesh has most often in the catalogue's houses: a kit made lying down is stood up by its
    // house, and a part placed on its own stands so too.
    private void LearnUpright()
    {
        _uprightLearned = true;
        var seen = new Dictionary<string, Dictionary<Vector2, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var piece in _catalogue.Values.Where(piece => piece.Asset.StartsWith(FormsPath, StringComparison.Ordinal)))
        {
            if (!props.TryListParts(piece.Asset, out var parts) || parts.Count < 2)
                continue;

            foreach (var part in parts)
            {
                var tilt = new Vector2(MathF.Round(part.Rotation.X), MathF.Round(part.Rotation.Z));
                var counts = seen.TryGetValue(part.Mesh, out var known) ? known : seen[part.Mesh] = [];
                counts[tilt] = counts.GetValueOrDefault(tilt) + 1;
            }
        }

        foreach (var (mesh, counts) in seen)
        {
            var most = counts.MaxBy(count => count.Value).Key;
            if (most != Vector2.Zero)
                _upright[mesh] = most;
        }
    }

    // The box a mesh's box fills once turned by the rotator (pitch, yaw, roll), as the game's FRotationMatrix turns it.
    private static PropBounds Tilted(PropBounds bounds, Vector3 rotator)
    {
        float sp = MathF.Sin(rotator.X / DegreesPerRadian), cp = MathF.Cos(rotator.X / DegreesPerRadian);
        float sy = MathF.Sin(rotator.Y / DegreesPerRadian), cy = MathF.Cos(rotator.Y / DegreesPerRadian);
        float sr = MathF.Sin(rotator.Z / DegreesPerRadian), cr = MathF.Cos(rotator.Z / DegreesPerRadian);
        var x = new Vector3(cp * cy, cp * sy, sp);
        var y = new Vector3(sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, -sr * cp);
        var z = new Vector3(-(cr * sp * cy + sr * sy), cy * sr - cr * sp * sy, cr * cp);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var cx in new[] { bounds.Min.X, bounds.Max.X })
        foreach (var cyy in new[] { bounds.Min.Y, bounds.Max.Y })
        foreach (var cz in new[] { bounds.Min.Z, bounds.Max.Z })
        {
            var turned = x * cx + y * cyy + z * cz;
            min = Vector3.Min(min, turned);
            max = Vector3.Max(max, turned);
        }

        return new PropBounds(min, max);
    }

    private void Start()
    {
        building.Changed += RefreshSelection;
        view.Moved += FollowView;
    }

    private void Stop()
    {
        building.Changed -= RefreshSelection;
        view.Moved -= FollowView;
    }

    private void FollowView()
    {
        ShadeAll(_plotShape);
        ShadeAll(_choiceBoxes);
        ShadeAll(_markShape);
        if (_drag != ArchitectDrag.None && _lastPoint is { } point)
            Point(point.X, point.Y);
    }

    // The picked piece's outline and box follow it after the server moved it, and go with it. A piece just placed is
    // picked once it stands.
    private void RefreshSelection()
    {
        if (_pickWhenPlaced is { } placed && _pickAsset is { } asset && building.TryFindAt(asset, placed.Position, SameSpot, _pickAbove, out var id)
            && FindKey(asset) is { } key)
        {
            _pickWhenPlaced = null;
            _pickAsset = null;
            if (_drag == ArchitectDrag.None)
            {
                Select(id, key);
                return;
            }
        }

        if (_selected == 0 || _drag is ArchitectDrag.MovePiece or ArchitectDrag.AxisMove)
            return;

        if (!building.TryFindPiece(_selected, out var prop))
        {
            Deselect();
            return;
        }

        if (_expected is { } expected && Vector3.Distance(prop.Position, expected.Position) <= SameSpot
            && MathF.Abs(prop.Rotation.Y - expected.Yaw) <= SameTurn)
            _expected = null;

        DrawSelection();
    }

    private readonly record struct Colour(byte Red, byte Green, byte Blue)
    {
        public static readonly Colour Plot = new(70, 220, 110);
        public static readonly Colour AxisX = new(255, 90, 80);
        public static readonly Colour AxisY = new(100, 255, 120);
        public static readonly Colour AxisZ = new(100, 170, 255);
        public static readonly Colour Fits = new(90, 210, 120);
        public static readonly Colour Refused = new(225, 70, 60);
        public static readonly Colour Picked = new(150, 255, 150);
    }
}
