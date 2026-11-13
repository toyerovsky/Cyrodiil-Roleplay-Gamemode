using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Jobs;

// A Jobs foreman's window: wages owed, what they sell and buy. The server keeps it up to date and does every deal.
[Service]
internal sealed partial class ForemanOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "foreman";

    private PageWindow? _window;

    // Another page taking the input closes the window, so a deal never goes on unseen.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.ForemanLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: rpc.SendForemanClose);

    private void Start()
    {
        rpc.ForemanChanged += Window.Deliver;
        rpc.ForemanAnswered += OnAnswered;
        rpc.ForemanClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.ForemanChanged -= Window.Deliver;
        rpc.ForemanAnswered -= OnAnswered;
        rpc.ForemanClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnAnswered(string text, bool done)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, done);
    }

    [ReceiveFromCef]
    private void OnForemanReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnForemanDeal(BrowserHandle browser, string offer, int count) => rpc.SendForemanDeal(offer, count);

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnForemanClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendForemanClose();
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, string text, bool done);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
