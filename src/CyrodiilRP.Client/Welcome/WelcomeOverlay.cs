using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Welcome;

// Holds the mouse and keyboard, taken from any other page, whenever there is a world to hold them in, until the player
// takes their character over or starts a new one.
[Service]
internal sealed partial class WelcomeOverlay(
    CefHost cef,
    WebPages pages,
    PageInput input,
    RpcHandlers rpc,
    PageLocale locale,
    PawnWatch pawn)
{
    private const string PageName = "welcome";

    private OverlayPage? _page;

    // The welcome as the server sent it, until the player has chosen.
    private string? _view;

    // The pawn (PawnWatch.Number) the page was last shown for, none while it is hidden.
    private int? _shownFor;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.WelcomeLayer, SendLocale);

    private void Start()
    {
        rpc.WelcomeShown += OnShown;
        rpc.WelcomeClosed += Close;
        rpc.NamingStarted += OnNamingStarted;
        locale.Changed += Page.ApplyLocale;
    }

    private void Stop()
    {
        rpc.WelcomeShown -= OnShown;
        rpc.WelcomeClosed -= Close;
        rpc.NamingStarted -= OnNamingStarted;
        locale.Changed -= Page.ApplyLocale;
        Close();
    }

    private void OnShown(string view)
    {
        _view = view;
        var page = Page.Open();
        if (Page.IsReady)
            SendWelcome(page.Browser, view);
    }

    // The name form comes next and takes the input from this page as it shows, so the game never shows in between.
    private void OnNamingStarted() => _view = null;

    private void Close()
    {
        _view = null;
        if (Page.Opened is not { } page)
            return;

        Hide(page);
        input.Release(page);
    }

    // The game builds its overlay anew after every load, so the page comes up again once the new pawn has settled.
    private void Update()
    {
        if (_view is null || Page.Opened is not { } page || !Page.IsReady
            || !pawn.HasSettled(WebPages.OverlaySettleSeconds))
            return;

        if (!input.IsHeldBy(page))
            Take(page);
        else if (_shownFor != pawn.Number)
        {
            cef.SetVisible(page.Viewport, true);
            _shownFor = pawn.Number;
        }
    }

    private void Take(WebPage page)
    {
        input.Take(page, lost: () => Hide(page));
        _shownFor = pawn.Number;
    }

    private void Hide(WebPage page)
    {
        _shownFor = null;
        cef.SetVisible(page.Viewport, false);
        if (_view is null && Page.IsReady)
            SendClosed(page.Browser);
    }

    // A reloaded page starts empty and unfocused: it gets the welcome and the keyboard again.
    [ReceiveFromCef]
    private void OnWelcomeReady(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        if (_view is { } view)
            SendWelcome(browser, view);
        if (Page.Opened is { } page && input.IsHeldBy(page))
            Take(page);
    }

    [ReceiveFromCef]
    private void OnWelcomePlay(BrowserHandle browser) => rpc.SendPlayCharacter();

    [ReceiveFromCef]
    private void OnWelcomeNew(BrowserHandle browser) => rpc.SendNewCharacter();

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendWelcome(BrowserHandle browser, string view);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
