using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Auction;

[Service]
internal sealed partial class AuctionOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "auction";

    private PageWindow? _window;

    // From the server's opening of the window until it or the player closes it. The server shows every open window the
    // lots again as anyone changes them, so an update in flight as the player closes the window must not open it again.
    private bool _wanted;

    // Another page taking the input closes the window, so nothing is bought unseen.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.AuctionLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: Leave);

    private void Start()
    {
        rpc.AuctionOpened += OnOpened;
        rpc.AuctionChanged += OnChanged;
        rpc.AuctionAnswered += OnAnswered;
        rpc.AuctionClosed += OnClosed;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.AuctionOpened -= OnOpened;
        rpc.AuctionChanged -= OnChanged;
        rpc.AuctionAnswered -= OnAnswered;
        rpc.AuctionClosed -= OnClosed;
        locale.Changed -= Window.ApplyLocale;
        OnClosed();
    }

    private void OnOpened(string json)
    {
        _wanted = true;
        Window.Deliver(json);
    }

    private void OnChanged(string json)
    {
        if (_wanted)
            Window.Deliver(json);
    }

    private void OnClosed()
    {
        _wanted = false;
        Window.Close();
    }

    private void Leave()
    {
        _wanted = false;
        rpc.SendAuctionClose();
    }

    private void OnAnswered(string text, bool done)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, done);
    }

    [ReceiveFromCef]
    private void OnAuctionReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnAuctionSearch(BrowserHandle browser, string text, string category, int page) => rpc.SendAuctionSearch(text, category, page);

    [ReceiveFromCef]
    private void OnAuctionPost(BrowserHandle browser, string item, int count, int startPrice, int buyoutPrice, int hours) =>
        rpc.SendAuctionPost(item, count, startPrice, buyoutPrice, hours);

    [ReceiveFromCef]
    private void OnAuctionBid(BrowserHandle browser, long lot, int amount) => rpc.SendAuctionBid(lot, amount);

    [ReceiveFromCef]
    private void OnAuctionBuy(BrowserHandle browser, long lot, int price) => rpc.SendAuctionBuy(lot, price);

    [ReceiveFromCef]
    private void OnAuctionWithdraw(BrowserHandle browser, long lot) => rpc.SendAuctionWithdraw(lot);

    [ReceiveFromCef]
    private void OnAuctionCollect(BrowserHandle browser, long parcel) => rpc.SendAuctionCollect(parcel);

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnAuctionClose(BrowserHandle browser)
    {
        Window.Close();
        Leave();
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
