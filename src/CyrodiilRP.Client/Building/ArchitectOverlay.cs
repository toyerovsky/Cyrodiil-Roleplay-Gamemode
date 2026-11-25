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
// The server's state opens it. While it holds the input the character stands still. B lets the game have the mouse, keys
// and camera again to walk and look, the page folded to a line, and B brings it back.
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

    private OverlayPage? _page;

    private bool _open;

    private bool _looking;

    private string? _pending;

    private long _plot;

    private ArchitectTool _tool;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.ArchitectLayer, SendLocale);

    private void Start()
    {
        rpc.ArchitectCatalogueArrived += world.SetCatalogue;
        rpc.ArchitectChanged += OnChanged;
        rpc.ArchitectAnswered += OnAnswered;
        rpc.ArchitectClosed += Close;
        rpc.PlotsArrived += OnPlotsArrived;
        world.FitChanged += OnFitChanged;
        world.Refused += OnRefused;
        world.SelectionChanged += OnSelectionChanged;
        world.MarkChanged += OnMarkChanged;
        pictures.Drawn += OnPictureDrawn;
        locale.Changed += Page.ApplyLocale;

        // Polled, as the mode's other keys, and only while architect mode is open.
        input.RegisterKeyBind(Key.B, OnLookKey, KeySource.Polled);
    }

    private void Stop()
    {
        rpc.ArchitectCatalogueArrived -= world.SetCatalogue;
        rpc.ArchitectChanged -= OnChanged;
        rpc.ArchitectAnswered -= OnAnswered;
        rpc.ArchitectClosed -= Close;
        rpc.PlotsArrived -= OnPlotsArrived;
        world.FitChanged -= OnFitChanged;
        world.Refused -= OnRefused;
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

        if (!_looking)
            Hold(opened);

        cef.SetVisible(opened.Viewport, true);
        SendState(opened.Browser, json);
    }

    // The pictures are drawn while the page is up to show them.
    private void Update() => pictures.Wanted = _open && !_looking;

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
        _looking = false;
        _pending = null;
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

    private void OnLookKey()
    {
        if (!_open || chat.IsOpen || Page.Opened is not { } opened || (pageInput.IsHeld && !pageInput.IsHeldBy(opened)))
            return;

        world.Cancel();
        _looking = !_looking;
        view.Pause(_looking);
        if (_looking)
            pageInput.Release(opened);
        else
            Hold(opened);

        SendLook(opened.Browser, _looking);
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
        SendStep(browser, world.Step);
        foreach (var (piece, picture) in pictures.ListDrawn())
            SendPicture(browser, piece, picture);
        if (_pending is not { } json || Page.Opened is not { } opened)
            return;

        _pending = null;
        if (!_looking)
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

    [ReceiveFromCef]
    private void OnArchitectCopy(BrowserHandle browser) => world.Copy();

    // Ctrl+V with the mouse at this point of the screen.
    [ReceiveFromCef]
    private void OnArchitectPaste(BrowserHandle browser, float x, float y) => world.Paste(x, y);

    [ReceiveFromCef]
    private void OnArchitectDuplicate(BrowserHandle browser) => world.Duplicate();

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

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, string text, bool done);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);

    [SendToCef]
    private partial void SendLook(BrowserHandle browser, bool looking);

    [SendToCef]
    private partial void SendFit(BrowserHandle browser, string fit);

    [SendToCef]
    private partial void SendRefused(BrowserHandle browser, string fit);

    [SendToCef]
    private partial void SendSelected(BrowserHandle browser, string piece);

    [SendToCef]
    private partial void SendMarked(BrowserHandle browser, int width, int depth);

    [SendToCef]
    private partial void SendStep(BrowserHandle browser, int step);

    // A piece's picture, by its key, as a PNG data URL.
    [SendToCef]
    private partial void SendPicture(BrowserHandle browser, string piece, string picture);
}
