using System.Numerics;
using System.Text.Json;
using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Pages;
using CyrodiilRP.Common.Building;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMpCSharpMod.Input;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Building;

// Architect mode's page, over the whole screen: the drawer, the plot's form and what the mouse does over the world, which
// it hands to ArchitectWorld as points from 0 to 1 of the screen, and the keys and mouse that move ArchitectCamera's view.
// The server's state opens it. B gives the game its camera back, the player's own view: with nothing picked or carried the
// game has the mouse and keys to walk and look, middle mouse picks the piece under the crosshair and C opens the pieces,
// and the page holds the input again while the player edits (it says so, ArchitectWantsInput). B goes back to the view
// over the plot.
[Service]
internal sealed partial class ArchitectOverlay(
    CefHost cef,
    WebPages pages,
    PageInput pageInput,
    RpcHandlers rpc,
    PageLocale locale,
    IInput input,
    ChatOverlay chat,
    ArchitectWorld world,
    ArchitectCamera view,
    PiecePictures pictures,
    ILogger logger)
{
    private const string PageName = "architect";
    private const char KeySeparator = ',';

    // The middle of the screen, where the game's crosshair is, in shares of it.
    private const float Crosshair = 0.5f;

    // How far a point of the gizmo moves, in shares of the screen, before the page draws it again: about a pixel.
    private const float GizmoSlack = 0.0005f;

    private OverlayPage? _page;

    private bool _open;

    private bool _playerView;

    // What the page last said: it needs the mouse and keys (a piece picked or carried, the pieces open, a dialog).
    private bool _pageWantsInput;

    private string? _pending;

    // The drawer's pieces as the server sent them, in parts, kept for a page that is not ready yet.
    private readonly List<string> _pieces = [];

    private long _plot;

    private ArchitectTool _tool;

    // The gizmo as the page last drew it, null for none.
    private GizmoOnScreen? _shownGizmo;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.ArchitectLayer, SendLocale);

    private void Start()
    {
        rpc.ArchitectCatalogueArrived += world.SetCatalogue;
        rpc.ArchitectChanged += OnChanged;
        rpc.ArchitectPiecesArrived += OnPiecesArrived;
        rpc.ArchitectAnswered += OnAnswered;
        rpc.ArchitectClosed += Close;
        rpc.PlotsArrived += OnPlotsArrived;
        world.FitChanged += OnFitChanged;
        world.Refused += OnRefused;
        world.BreakRefused += OnBreakRefused;
        world.SelectionChanged += OnSelectionChanged;
        world.MarkChanged += OnMarkChanged;
        pictures.Drawn += OnPictureDrawn;
        locale.Changed += Page.ApplyLocale;

        // Polled, as the mode's other keys, and only while architect mode is open.
        input.RegisterKeyBind(Key.B, OnPlayerViewKey, KeySource.Polled);
        input.RegisterKeyBind(Key.MIDDLE_MOUSE_BUTTON, OnPickKey, KeySource.Polled);
        input.RegisterKeyBind(Key.C, () => WhilePlaying(opened => SendOpenCatalogue(opened.Browser)), KeySource.Polled);
        input.RegisterKeyBind(Key.L, () => WhilePlaying(opened => SendToggleHidden(opened.Browser)), KeySource.Polled);
    }

    private void Stop()
    {
        rpc.ArchitectCatalogueArrived -= world.SetCatalogue;
        rpc.ArchitectChanged -= OnChanged;
        rpc.ArchitectPiecesArrived -= OnPiecesArrived;
        rpc.ArchitectAnswered -= OnAnswered;
        rpc.ArchitectClosed -= Close;
        rpc.PlotsArrived -= OnPlotsArrived;
        world.FitChanged -= OnFitChanged;
        world.Refused -= OnRefused;
        world.BreakRefused -= OnBreakRefused;
        world.SelectionChanged -= OnSelectionChanged;
        world.MarkChanged -= OnMarkChanged;
        pictures.Drawn -= OnPictureDrawn;
        locale.Changed -= Page.ApplyLocale;
        Close();
    }

    private void OnChanged(string json, long plot, string toolName)
    {
        if (!Enum.TryParse<ArchitectTool>(toolName, out var tool))
        {
            logger.LogError("[CyrodiilRP] The server opened architect mode with the tool '{Tool}', which is none.", toolName);
            return;
        }

        if (!_open || plot != _plot || tool != _tool)
        {
            world.Begin(plot, tool);
            view.Begin(plot);
        }

        _plot = plot;
        _tool = tool;
        _open = true;
        var opened = Page.Open();
        if (!Page.IsReady)
        {
            _pending = json;
            return;
        }

        Hold(opened);

        cef.SetVisible(opened.Viewport, true);
        SendState(opened.Browser, json);
    }

    private void OnPiecesArrived(string json, bool first)
    {
        if (first)
            _pieces.Clear();
        _pieces.Add(json);
        if (Page.IsReady && Page.Opened is { } opened)
            SendPieces(opened.Browser, json, first);
    }

    // The pictures are drawn while the page is up to show them. The gizmo is drawn by the page, over everything, where
    // the view shows it this frame.
    private void Update()
    {
        pictures.Wanted = _open;
        if (!_open || !Page.IsReady || Page.Opened is not { } opened)
            return;

        GizmoOnScreen? gizmo = world.TryProjectGizmo(out var projected) ? projected : null;
        if (IsSame(gizmo, _shownGizmo))
            return;

        _shownGizmo = gizmo;
        SendGizmo(opened.Browser, gizmo is { } shown
            ? JsonSerializer.Serialize(new GizmoView(Numbers(shown.Origin), Numbers(shown.X), Numbers(shown.Y), Numbers(shown.Z)), GizmoJson.Default.GizmoView)
            : string.Empty);
    }

    private static bool IsSame(GizmoOnScreen? one, GizmoOnScreen? other)
    {
        if (one is not { } a || other is not { } b)
            return one is null && other is null;

        return IsNear(a.Origin, b.Origin) && IsNear(a.X, b.X) && IsNear(a.Y, b.Y) && IsNear(a.Z, b.Z);
    }

    private static bool IsNear(Vector2? one, Vector2? other)
    {
        if (one is not { } a || other is not { } b)
            return one is null && other is null;

        return MathF.Abs(a.X - b.X) <= GizmoSlack && MathF.Abs(a.Y - b.Y) <= GizmoSlack;
    }

    private static float[] Numbers(Vector2 point) => [point.X, point.Y];

    private static float[]? Numbers(Vector2? point) => point is { } at ? Numbers(at) : null;

    private void OnPictureDrawn(string piece, string picture)
    {
        if (_open && Page.IsReady && Page.Opened is { } opened)
            SendPicture(opened.Browser, piece, picture);
    }

    private void OnPlotsArrived(string signAsset, long[] plots, float[] numbers)
    {
        if (_open)
            world.DrawPlot();
    }

    private void Close()
    {
        if (!_open)
            return;

        _open = false;
        _playerView = false;
        _pageWantsInput = false;
        _pending = null;
        _shownGizmo = null;
        _plot = 0;
        world.End();
        view.End();
        if (Page.Opened is not { } opened)
            return;

        SendClosed(opened.Browser);
        pageInput.Release(opened);
        cef.SetVisible(opened.Viewport, false);
    }

    // Another page took the input: architect mode goes, and the server hears it.
    private void Hold(WebPage opened) => pageInput.Take(opened, lost: () =>
    {
        Close();
        rpc.SendArchitectClose();
    });

    private void OnPlayerViewKey()
    {
        if (!_open || chat.IsOpen || Page.Opened is not { } opened || (pageInput.IsHeld && !pageInput.IsHeldBy(opened)))
            return;

        world.Cancel();
        _playerView = !_playerView;
        view.UsePlayerView(_playerView);
        SendPlayerView(opened.Browser, _playerView);
        ApplyInput(opened);
    }

    // In the player's own view the page holds the input only while the player edits: otherwise the game has it.
    private void ApplyInput(WebPage opened)
    {
        if (!_playerView || _pageWantsInput)
        {
            if (!pageInput.IsHeldBy(opened))
                Hold(opened);
            return;
        }

        if (pageInput.IsHeldBy(opened))
            pageInput.Release(opened);
    }

    [ReceiveFromCef]
    private void OnArchitectWantsInput(BrowserHandle browser, bool wants)
    {
        _pageWantsInput = wants;
        if (_open && Page.Opened is { } opened)
            ApplyInput(opened);
    }

    // Middle mouse in the player's own view: the piece under the crosshair is picked, and the page takes the mouse to edit it.
    private void OnPickKey() => WhilePlaying(_ => world.PickAt(Crosshair, Crosshair));

    // A key of the player's own view, only while the game has the input there.
    private void WhilePlaying(Action<WebPage> act)
    {
        if (_open && _playerView && !chat.IsOpen && Page.Opened is { } opened && !pageInput.IsHeld)
            act(opened);
    }

    private void OnAnswered(string text, bool done)
    {
        if (!done)
            world.ForgetExpected();

        if (_open && Page.Opened is { } opened)
            SendAnswer(opened.Browser, text, done);
    }

    private void OnFitChanged(GhostFit fit)
    {
        if (_open && Page.Opened is { } opened)
            SendFit(opened.Browser, fit.ToString());
    }

    private void OnRefused(GhostFit fit)
    {
        if (_open && Page.Opened is { } opened)
            SendRefused(opened.Browser, fit.ToString());
    }

    private void OnBreakRefused(BreakRefusal why)
    {
        if (_open && Page.Opened is { } opened)
            SendBreakRefused(opened.Browser, why.ToString());
    }

    private void OnSelectionChanged(string piece)
    {
        if (_open && Page.Opened is { } opened)
            SendSelected(opened.Browser, piece);
    }

    private void OnMarkChanged(int width, int depth)
    {
        if (_open && Page.Opened is { } opened)
            SendMarked(opened.Browser, width, depth);
    }

    [ReceiveFromCef]
    private void OnArchitectReady(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        _shownGizmo = null;
        SendStep(browser, world.Step);
        for (var index = 0; index < _pieces.Count; index++)
            SendPieces(browser, _pieces[index], first: index == 0);
        foreach (var (piece, picture) in pictures.ListDrawn())
            SendPicture(browser, piece, picture);
        if (_pending is not { } json || Page.Opened is not { } opened)
            return;

        _pending = null;
        Hold(opened);

        cef.SetVisible(opened.Viewport, true);
        SendState(browser, json);
    }

    [ReceiveFromCef]
    private void OnArchitectGrab(BrowserHandle browser, float x, float y, string piece) => world.Grab(x, y, piece);

    [ReceiveFromCef]
    private void OnArchitectPoint(BrowserHandle browser, float x, float y) => world.Point(x, y);

    [ReceiveFromCef]
    private void OnArchitectDrop(BrowserHandle browser, float x, float y) => world.Drop(x, y);

    [ReceiveFromCef]
    private void OnArchitectCancel(BrowserHandle browser) => world.Cancel();

    [ReceiveFromCef]
    private void OnArchitectTurn(BrowserHandle browser, float degrees) => world.Turn(degrees);

    [ReceiveFromCef]
    private void OnArchitectRaise(BrowserHandle browser, float centimetres) => world.Raise(centimetres);

    [ReceiveFromCef]
    private void OnArchitectStep(BrowserHandle browser) => SendStep(browser, world.NextStep());

    [ReceiveFromCef]
    private void OnArchitectSetStep(BrowserHandle browser, int steps) => SendStep(browser, world.SetStep(steps));

    // The panel's Height by, in centimetres.
    [ReceiveFromCef]
    private void OnArchitectSetHeight(BrowserHandle browser, int centimetres) => world.HeightStep = Math.Max(0, centimetres);

    // The panel's Stand on: what is under (true) or the plot's ground.
    [ReceiveFromCef]
    private void OnArchitectSetSettle(BrowserHandle browser, bool settle) => world.Settle = settle;

    // An arrow: a grid step forward (1) or back (-1), right (1) or left (-1), as the camera looks.
    [ReceiveFromCef]
    private void OnArchitectNudge(BrowserHandle browser, int forward, int right) => world.Nudge(forward, right);

    [ReceiveFromCef]
    private void OnArchitectDelete(BrowserHandle browser) => world.RemoveSelected();

    // A plot of the list to build on, or 0 to choose again.
    [ReceiveFromCef]
    private void OnArchitectChoose(BrowserHandle browser, long plot) => rpc.SendArchitectChoose(plot);

    [ReceiveFromCef]
    private void OnArchitectNewPlot(BrowserHandle browser) => rpc.SendArchitectNewPlot();

    // Ctrl+Z or Undo on the page: the server takes back the newest change on the plot.
    [ReceiveFromCef]
    private void OnArchitectUndo(BrowserHandle browser) => rpc.SendArchitectUndo();

    // A plot to take away, by its id, once the page asked if it should be.
    [ReceiveFromCef]
    private void OnArchitectRemovePlot(BrowserHandle browser, long plot) => rpc.SendArchitectRemovePlot(plot);

    [ReceiveFromCef]
    private void OnArchitectCopy(BrowserHandle browser) => world.Copy();

    // Ctrl+V with the mouse at this point of the screen.
    [ReceiveFromCef]
    private void OnArchitectPaste(BrowserHandle browser, float x, float y) => world.Paste(x, y);

    [ReceiveFromCef]
    private void OnArchitectDuplicate(BrowserHandle browser) => world.Duplicate();

    [ReceiveFromCef]
    private void OnArchitectBreakApart(BrowserHandle browser) => world.BreakApart();

    // The keys of the pieces the drawer shows now, joined by commas: their pictures are drawn first.
    [ReceiveFromCef]
    private void OnArchitectShowing(BrowserHandle browser, string keys) => pictures.Prefer(keys.Split(KeySeparator, StringSplitOptions.RemoveEmptyEntries));

    // R or the button: the plot's front, and its sign, to the next side.
    [ReceiveFromCef]
    private void OnArchitectTurnFront(BrowserHandle browser) => world.TurnFront();

    [ReceiveFromCef]
    private void OnArchitectSnapToFloor(BrowserHandle browser) => world.SnapToFloor();

    // WASD as held: forward (1) or back (-1), right (1) or left (-1), 0 for none.
    [ReceiveFromCef]
    private void OnArchitectPan(BrowserHandle browser, int forward, int right) => view.Pan(forward, right);

    // The right mouse button dragged over the world, in pixels across and down.
    [ReceiveFromCef]
    private void OnArchitectOrbit(BrowserHandle browser, float across, float down) => view.Orbit(across, down);

    // Notches of the wheel over the world, positive rolled towards the player.
    [ReceiveFromCef]
    private void OnArchitectZoom(BrowserHandle browser, int notches) => view.Zoom(notches);

    [ReceiveFromCef]
    private void OnArchitectDeselect(BrowserHandle browser) => world.Deselect();

    // The plot marked, with the form as the page wrote it (a PlotForm as JSON).
    [ReceiveFromCef]
    private void OnArchitectSavePlot(BrowserHandle browser, string form)
    {
        if (!world.SaveMarked(form))
            SendMarked(browser, 0, 0);
    }

    [ReceiveFromCef]
    private void OnArchitectClearPlot(BrowserHandle browser) => world.ClearMark();

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnArchitectLeave(BrowserHandle browser)
    {
        Close();
        rpc.SendArchitectClose();
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    // A part of the drawer's pieces, as the server sent it.
    [SendToCef]
    private partial void SendPieces(BrowserHandle browser, string json, bool first);

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, string text, bool done);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);

    [SendToCef]
    private partial void SendPlayerView(BrowserHandle browser, bool playerView);

    [SendToCef]
    private partial void SendOpenCatalogue(BrowserHandle browser);

    [SendToCef]
    private partial void SendToggleHidden(BrowserHandle browser);

    [SendToCef]
    private partial void SendFit(BrowserHandle browser, string fit);

    [SendToCef]
    private partial void SendRefused(BrowserHandle browser, string fit);

    // Why the picked piece cannot be taken apart: a BreakRefusal by name.
    [SendToCef]
    private partial void SendBreakRefused(BrowserHandle browser, string why);

    [SendToCef]
    private partial void SendSelected(BrowserHandle browser, string piece);

    [SendToCef]
    private partial void SendMarked(BrowserHandle browser, int width, int depth);

    [SendToCef]
    private partial void SendStep(BrowserHandle browser, int step);

    // The gizmo where the screen shows it, as GizmoView's JSON, or empty for none.
    [SendToCef]
    private partial void SendGizmo(BrowserHandle browser, string json);

    // A piece's picture, by its key, as a PNG data URL.
    [SendToCef]
    private partial void SendPicture(BrowserHandle browser, string piece, string picture);
}
