using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Guide;

// A guide's card holds the input as a window does. Another page taking it closes the card as Esc would, so what waited
// behind the card does not open over that page.
[Service]
internal sealed partial class GuideOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale, PawnWatch pawn)
{
    private const string PageName = "guides";

    private PageWindow? _window;

    // A card that came before the character's pawn settled (the welcome as a load ends) waits for it.
    private string? _waiting;

    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.GuideLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: () => rpc.SendGuideClose(true));

    private void Start()
    {
        rpc.GuideShown += OnShown;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.GuideShown -= OnShown;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnShown(string json) => _waiting = json;

    private void Update()
    {
        if (_waiting is not { } json || !pawn.HasSettled(WebPages.OverlaySettleSeconds))
            return;

        _waiting = null;
        Window.Deliver(json);
    }

    [ReceiveFromCef]
    private void OnGuideReady(BrowserHandle browser) => Window.MarkReady(browser);

    // E, Enter or the button: what waited behind the card goes on.
    [ReceiveFromCef]
    private void OnGuideContinue(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendGuideContinue();
    }

    // Esc or the button.
    [ReceiveFromCef]
    private void OnGuideClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendGuideClose(false);
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
