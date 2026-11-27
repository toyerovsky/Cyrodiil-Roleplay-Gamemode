using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Building;

// A plot's sign window, opened by H in front of the sign: the plot, its price and tax, and Buy.
[Service]
internal sealed partial class PlotSaleOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "plot";

    private PageWindow? _window;

    // Another page taking the input closes the window, so a plot is never bought unseen.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.StableLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: rpc.SendPlotSaleClose);

    private void Start()
    {
        rpc.PlotSaleChanged += Window.Deliver;
        rpc.PlotSaleAnswered += OnAnswered;
        rpc.PlotSaleClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.PlotSaleChanged -= Window.Deliver;
        rpc.PlotSaleAnswered -= OnAnswered;
        rpc.PlotSaleClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnAnswered(string text, bool bought)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, bought);
    }

    [ReceiveFromCef]
    private void OnPlotReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnPlotBuy(BrowserHandle browser) => rpc.SendPlotSaleBuy();

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnPlotClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendPlotSaleClose();
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
