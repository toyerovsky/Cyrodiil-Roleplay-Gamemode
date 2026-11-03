using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Traders;

[Service]
internal sealed partial class TraderOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "trader";

    private PageWindow? _window;

    // Another page taking the input closes the window, so a purchase never goes on unseen.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.TraderLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: rpc.SendTraderClose);

    private void Start()
    {
        rpc.TraderChanged += Window.Deliver;
        rpc.TraderAnswered += OnAnswered;
        rpc.TraderClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.TraderChanged -= Window.Deliver;
        rpc.TraderAnswered -= OnAnswered;
        rpc.TraderClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnAnswered(string text, bool bought)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, bought);
    }

    [ReceiveFromCef]
    private void OnTraderReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnTraderBuy(BrowserHandle browser, string ware, int count) => rpc.SendTraderBuy(ware, count);

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnTraderClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendTraderClose();
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
