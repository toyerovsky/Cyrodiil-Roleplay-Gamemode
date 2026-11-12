using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Notices;

[Service]
internal sealed partial class BoardOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "notices";

    private PageWindow? _window;

    // Another page taking the input closes the window, so the server stops sending it the board.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.BoardLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: rpc.SendBoardClose);

    private void Start()
    {
        rpc.BoardChanged += Window.Deliver;
        rpc.BoardAnswered += OnAnswered;
        rpc.BoardClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.BoardChanged -= Window.Deliver;
        rpc.BoardAnswered -= OnAnswered;
        rpc.BoardClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnAnswered(string text, bool done)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, done);
    }

    [ReceiveFromCef]
    private void OnBoardReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnBoardPost(BrowserHandle browser, string text) => rpc.SendBoardPost(text);

    [ReceiveFromCef]
    private void OnBoardTakeDown(BrowserHandle browser, long notice) => rpc.SendBoardTakeDown(notice);

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnBoardClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendBoardClose();
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
