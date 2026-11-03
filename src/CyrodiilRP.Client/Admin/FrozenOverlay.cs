using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Admin;

// A freeze holds the player's mouse and keyboard on this page, so the game gets no input to walk or turn with. The page
// takes them whenever no other page holds them, in the world.
[Service]
internal sealed partial class FrozenOverlay(
    CefHost cef,
    WebPages pages,
    PageInput pageInput,
    RpcHandlers rpc,
    PageLocale locale,
    PawnWatch pawn)
{
    private const string PageName = "frozen";

    private OverlayPage? _page;

    // The notice while frozen, null otherwise.
    private string? _notice;

    // The pawn (PawnWatch.Number) the page was last shown for, none while it is hidden.
    private int? _shownFor;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.FrozenLayer, SendLocale);

    private void Start()
    {
        rpc.FrozenByStaff += OnFrozen;
        rpc.ThawedByStaff += OnThawed;
        locale.Changed += Page.ApplyLocale;
    }

    private void Stop()
    {
        rpc.FrozenByStaff -= OnFrozen;
        rpc.ThawedByStaff -= OnThawed;
        locale.Changed -= Page.ApplyLocale;
        OnThawed();
    }

    private void OnFrozen(string notice)
    {
        _notice = notice;
        var page = Page.Open();
        if (Page.IsReady)
            SendNotice(page.Browser, notice);
    }

    private void OnThawed()
    {
        _notice = null;
        if (Page.Opened is not { } page)
            return;

        if (Page.IsReady)
            SendThawed(page.Browser);

        _shownFor = null;
        cef.SetVisible(page.Viewport, false);
        pageInput.Release(page);
    }

    // The game builds its overlay anew after every load, so the page comes up again once the new pawn has settled.
    private void Update()
    {
        if (_notice is null || Page.Opened is not { } page || !Page.IsReady
            || !pawn.HasSettled(WebPages.OverlaySettleSeconds))
            return;

        if (!pageInput.IsHeld)
        {
            pageInput.Take(page, lost: () => { });
            _shownFor = pawn.Number;
        }
        else if (pageInput.IsHeldBy(page) && _shownFor != pawn.Number)
        {
            cef.SetVisible(page.Viewport, true);
            _shownFor = pawn.Number;
        }
    }

    [ReceiveFromCef]
    private void OnFrozenReady(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        if (_notice is { } notice)
            SendNotice(browser, notice);
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendNotice(BrowserHandle browser, string notice);

    [SendToCef]
    private partial void SendThawed(BrowserHandle browser);
}
