using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Away;

// The AFK screen over the game, which never takes the input: up while the server warns the player or marks them away.
// The page counts the seconds on by itself from the number it is sent, and the server ends it at the player's first sign.
[Service]
internal sealed partial class AwayOverlay(CefHost cef, WebPages pages, RpcHandlers rpc, PageLocale locale, PawnWatch pawn)
{
    private const string PageName = "away";

    private OverlayPage? _page;

    // What the screen shows, none while it is down.
    private AwayScreen? _shown;

    // The pawn (PawnWatch.Number) the page was last shown for: the game builds its overlay anew after every load.
    private int? _shownFor;

    public bool IsShown => _shown is not null;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.AwayLayer, SendLocale);

    private void Start()
    {
        rpc.AwayWarned += OnWarned;
        rpc.AwayMarked += OnMarked;
        rpc.AwayEnded += OnEnded;
        locale.Changed += Page.ApplyLocale;
    }

    private void Stop()
    {
        rpc.AwayWarned -= OnWarned;
        rpc.AwayMarked -= OnMarked;
        rpc.AwayEnded -= OnEnded;
        locale.Changed -= Page.ApplyLocale;
        OnEnded();
    }

    private void OnWarned(int secondsLeft) => Show(new AwayScreen(ScreenKind.Warning, secondsLeft, Time.Elapsed));

    private void OnMarked(int secondsAway) => Show(new AwayScreen(ScreenKind.Away, secondsAway, Time.Elapsed));

    private void Show(AwayScreen screen)
    {
        _shown = screen;
        _shownFor = null;
        var page = Page.Open();
        if (Page.IsReady)
            Send(page.Browser, screen);
    }

    private void OnEnded()
    {
        _shown = null;
        _shownFor = null;
        if (Page.Opened is not { } page)
            return;

        if (Page.IsReady)
            SendEnded(page.Browser);

        cef.SetVisible(page.Viewport, false);
    }

    private void Update()
    {
        if (_shown is null || Page.Opened is not { } page || !Page.IsReady || _shownFor == pawn.Number
            || !pawn.HasSettled(WebPages.OverlaySettleSeconds))
            return;

        cef.SetVisible(page.Viewport, true);
        _shownFor = pawn.Number;
    }

    [ReceiveFromCef]
    private void OnAwayReady(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        if (_shown is { } screen)
            Send(browser, screen);
    }

    // The seconds as they are now, for a page that loaded or reloaded after the server's word.
    private void Send(BrowserHandle browser, AwayScreen screen)
    {
        var passed = (int)(Time.Elapsed - screen.At);
        Action send = screen.Kind switch
        {
            ScreenKind.Warning => () => SendWarning(browser, Math.Max(0, screen.Seconds - passed)),
            ScreenKind.Away => () => SendMarked(browser, screen.Seconds + passed),
        };
        send();
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendWarning(BrowserHandle browser, int seconds);

    [SendToCef]
    private partial void SendMarked(BrowserHandle browser, int seconds);

    [SendToCef]
    private partial void SendEnded(BrowserHandle browser);

    private enum ScreenKind
    {
        // Counts down to the mark.
        Warning,

        // Counts the time away up.
        Away,
    }

    // At: Time.Elapsed when the server's word came.
    private sealed record AwayScreen(ScreenKind Kind, int Seconds, float At);
}
