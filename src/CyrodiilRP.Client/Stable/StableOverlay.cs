using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Stable;

[Service]
internal sealed partial class StableOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "stable";

    private PageWindow? _window;

    // Another page taking the input closes the window, so a horse is never bought unseen.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.StableLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: rpc.SendStableClose);

    private void Start()
    {
        rpc.StableChanged += Window.Deliver;
        rpc.StableAnswered += OnAnswered;
        rpc.StableClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.StableChanged -= Window.Deliver;
        rpc.StableAnswered -= OnAnswered;
        rpc.StableClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnAnswered(string text, bool bought)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, bought);
    }

    [ReceiveFromCef]
    private void OnStableReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnStableBuy(BrowserHandle browser, string horse) => rpc.SendStableBuy(horse);

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnStableClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendStableClose();
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, string text, bool bought);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
