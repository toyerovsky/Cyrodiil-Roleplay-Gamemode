using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fisher;

[Service]
internal sealed partial class FisherOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "fisher";

    private PageWindow? _window;

    // Another page taking the input closes the window, so a deal never goes on unseen.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.FisherLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: rpc.SendFisherClose);

    private void Start()
    {
        rpc.FisherChanged += Window.Deliver;
        rpc.FisherAnswered += OnAnswered;
        rpc.FisherRecordsArrived += OnRecordsArrived;
        rpc.FisherClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.FisherChanged -= Window.Deliver;
        rpc.FisherAnswered -= OnAnswered;
        rpc.FisherRecordsArrived -= OnRecordsArrived;
        rpc.FisherClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnAnswered(string text, bool done)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, done);
    }

    private void OnRecordsArrived(string json)
    {
        if (Window.ShownBrowser is { } browser)
            SendRecords(browser, json);
    }

    [ReceiveFromCef]
    private void OnFisherReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnFisherBuy(BrowserHandle browser, string ware, int count) => rpc.SendFisherBuy(ware, count);

    [ReceiveFromCef]
    private void OnFisherSell(BrowserHandle browser, string item, int count) => rpc.SendFisherSell(item, count);

    [ReceiveFromCef]
    private void OnFisherSellAll(BrowserHandle browser) => rpc.SendFisherSellAll();

    [ReceiveFromCef]
    private void OnFisherRepair(BrowserHandle browser, string item) => rpc.SendFisherRepair(item);

    [ReceiveFromCef]
    private void OnFisherAskRecords(BrowserHandle browser) => rpc.SendFisherAskRecords();

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnFisherClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendFisherClose();
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, string text, bool done);

    [SendToCef]
    private partial void SendRecords(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
