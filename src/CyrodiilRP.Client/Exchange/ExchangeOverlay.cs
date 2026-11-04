using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Exchange;

[Service]
internal sealed partial class ExchangeOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "exchange";

    private PageWindow? _window;

    // Another page taking the input calls the trade off, so it never goes on unseen.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.ExchangeLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: rpc.SendExchangeCancel);

    private void Start()
    {
        rpc.ExchangeChanged += Window.Deliver;
        rpc.ExchangeRefused += OnRefused;
        rpc.ExchangeDone += OnDone;
        rpc.ExchangeClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.ExchangeChanged -= Window.Deliver;
        rpc.ExchangeRefused -= OnRefused;
        rpc.ExchangeDone -= OnDone;
        rpc.ExchangeClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnRefused(string text)
    {
        if (Window.ShownBrowser is { } browser)
            SendRefused(browser, text);
    }

    private void OnDone(string text)
    {
        if (Window.ShownBrowser is { } browser)
            SendDone(browser, text);
    }

    [ReceiveFromCef]
    private void OnExchangeReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnExchangeOffer(BrowserHandle browser, int gold) => rpc.SendExchangeOffer(gold);

    [ReceiveFromCef]
    private void OnExchangeOfferItem(BrowserHandle browser, string stack, int count) => rpc.SendExchangeOfferItem(stack, count);

    [ReceiveFromCef]
    private void OnExchangeConfirm(BrowserHandle browser, int version) => rpc.SendExchangeConfirm(version);

    [ReceiveFromCef]
    private void OnExchangeCancel(BrowserHandle browser) => rpc.SendExchangeCancel();

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendRefused(BrowserHandle browser, string text);

    [SendToCef]
    private partial void SendDone(BrowserHandle browser, string text);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
