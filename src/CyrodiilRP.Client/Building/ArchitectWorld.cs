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
internal sealed partial class ArchitectWorld(ILocalProps props, BuildingProps building, ArchitectCamera view, RpcHandlers rpc, ILogger logger)
{
    // How far the mouse reaches into the world, in centimetres.
    private const float Reach = 20_000f;

    // How high the outline of a plot being marked is drawn, in centimetres. The form sets the real height.
    private const float MarkHeight = 600f;

    private const float LineThickness = 4f;

    // How far an arrow moves the picked piece with the grid off, in centimetres.
    private const float FreeNudge = 10f;

    // A duplicate stands half a tile from its piece.
    private const float DuplicateSteps = 2f;

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
    private LocalPropHandle? _plotBox;
    private readonly List<LocalPropHandle> _choiceBoxes = [];

    // Where the picked piece was last sent, until the server stands it there: held keys move it on from there.
    private PieceSpot? _expected;
    // What Ctrl+C copied, for Ctrl+V.
    private CopiedPiece? _copied;
    // A piece sent to be placed, picked once the server stands it.
    private PieceSpot? _pickWhenPlaced;
    private string? _pickAsset;

    public event Action<GhostFit>? FitChanged;

    public event Action<string>? SelectionChanged;

    public event Action<int, int>? MarkChanged;

    // A turn or a lift of the picked piece that would not fit.
    public event Action<GhostFit>? Refused;

    // Steps of the grid a tile: 1, 2 or 4, or 0 for none.
    public int Step => Steps[_stepIndex];

    // A carried piece settles on what is under it, or stands on the plot's ground.
    public bool Settle { get; set; } = true;

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
        Remove(ref _plotBox);
        RemoveChoices();
        _plotId = 0;
        _tool = ArchitectTool.Choose;
        _marking = false;
    }

    // Again after the server's plots came, so a plot whose size changed is drawn as it is. Choosing, every plot around.
    public void DrawPlot()
    {
        Remove(ref _plotBox);
        RemoveChoices();
        switch (_tool)
        {
            case ArchitectTool.Choose:
                foreach (var id in building.PlotIds)
                {
                    if (building.TryFindPlot(id, out var each))
                        _choiceBoxes.Add(props.PlaceBox(Outline(each.Center, each.HalfSize, 0f, Colour.Plot)));
                }

                return;
            case ArchitectTool.Mark:
                return;
            case ArchitectTool.Build:
                if (building.TryFindPlot(_plotId, out var box))
                    _plotBox = props.PlaceBox(Outline(box.Center, box.HalfSize, 0f, Colour.Plot));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(_tool), _tool, "Not an architect tool.");
        }
    }

    private void RemoveChoices()
    {
        foreach (var box in _choiceBoxes)
            props.Remove(box);
        _choiceBoxes.Clear();
    }

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
            _pending = new PendingMove(picked, key, prop.Rotation.Y);
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
        _drag = ArchitectDrag.MovePiece;
        _carried = pending.Key;
        _moving = pending.Id;
        _yaw = pending.Yaw;
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
            default:
                throw new ArgumentOutOfRangeException(nameof(_drag), _drag, "Not an architect drag.");
        }
    }

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
                    Place(piece, spot, _yaw);
                Cancel();
                return;
            case ArchitectDrag.MovePiece:
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
        if (_drag == ArchitectDrag.MovePiece)
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

        _copied = new CopiedPiece(key, prop.Rotation.Y);
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
        _yaw = copied.Yaw;
        _raise = 0f;
        Point(x, y);
        if (_fit == GhostFit.Fits && _spot is { } spot)
            Place(copied.Key, spot, _yaw);
        else
            Refused?.Invoke(_fit == GhostFit.None ? GhostFit.NoGround : _fit);

        Cancel();
    }

    // A copy of the picked piece half a tile to its right as the camera looks, at its height and turn, picked once it
    // stands.
    public void Duplicate()
    {
        if (_drag != ArchitectDrag.None || !TryFindSelected(out var prop) || FindKey(prop.Asset) is not { } key
            || !_catalogue.TryGetValue(key, out var piece) || !TryFindGridAxes(out _, out var aside))
            return;

        Measure(piece);
        var by = aside * (BuildingGrid.TileCentimetres / DuplicateSteps);
        var to = prop.Position + new Vector3(by.X, by.Y, 0f);
        var fit = Judge(_catalogue[key], to, prop.Rotation.Y);
        if (fit != GhostFit.Fits)
        {
            Refused?.Invoke(fit);
            return;
        }

        Place(key, to, prop.Rotation.Y);
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

    // The server refused the last change: the picked piece is where the server last stood it.
    public void ForgetExpected() => _expected = null;

    private void Place(string key, Vector3 spot, float yaw)
    {
        rpc.SendArchitectPlace(key, [spot.X, spot.Y, spot.Z, yaw]);
        if (_catalogue.TryGetValue(key, out var piece))
        {
            _pickWhenPlaced = new PieceSpot(spot, yaw);
            _pickAsset = piece.Asset;
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
        SelectionChanged?.Invoke(string.Empty);
    }

    // The marked plot with the page's form. False with nothing marked.
    public bool SaveMarked(string form)
    {
        if (!_marking || _marked is not { } box)
            return false;

        var center = box.Center;
        var outward = new Vector2(_markStart.X - center.X, _markStart.Y - center.Y);
        var signYaw = outward == Vector2.Zero ? 0f : MathF.Atan2(outward.Y, outward.X) * 180f / MathF.PI;
        rpc.SendArchitectMarkPlot([box.FirstTileX, box.FirstTileY, box.Width, box.Depth], [box.GroundZ, _markStart.X, _markStart.Y, _markStart.Z, signYaw], form);
        return true;
    }

    public void ClearMark()
    {
        _marked = null;
        Remove(ref _ghostBox);
        MarkChanged?.Invoke(0, 0);
    }

    // The game measures a mesh the first time it is carried, and the server keeps the first measurement it gets.
    private void Measure(CataloguePiece piece)
    {
        if (piece.Bounds is not null || !props.TryMeasure(piece.Asset, piece.Scale, out var bounds))
            return;

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

        var prop = new Prop(piece.Asset, position, new Vector3(0f, _yaw, 0f), piece.Scale);
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

        var box = PlotBox.Between(_markStart, trace.Location, MathF.Min(_markStart.Z, trace.Location.Z), MarkHeight);
        _marked = box;
        Remove(ref _ghostBox);
        _ghostBox = props.PlaceBox(Outline(box.Center, box.HalfSize, 0f, Colour.Plot));
        MarkChanged?.Invoke(box.Width, box.Depth);
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

    private sealed record PendingMove(long Id, string Key, float Yaw);

    private sealed record PieceSpot(Vector3 Position, float Yaw);

    private sealed record CopiedPiece(string Key, float Yaw);

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
        if (_drag != ArchitectDrag.None && _lastPoint is { } point)
            Point(point.X, point.Y);
    }

    // The picked piece's outline and box follow it after the server moved it, and go with it. A piece just placed is
    // picked once it stands.
    private void RefreshSelection()
    {
        if (_pickWhenPlaced is { } placed && _pickAsset is { } asset && building.TryFindAt(asset, placed.Position, SameSpot, out var id)
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

        if (_selected == 0 || _drag == ArchitectDrag.MovePiece)
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
        public static readonly Colour Plot = new(230, 190, 90);
        public static readonly Colour Fits = new(90, 210, 120);
        public static readonly Colour Refused = new(225, 70, 60);
        public static readonly Colour Picked = new(150, 255, 150);
    }
}
