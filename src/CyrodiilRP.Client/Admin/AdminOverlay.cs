using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Admin;

[Service]
internal sealed partial class AdminOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "admin";

    private PageWindow? _window;

    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.AdminLayer, SendLocale),
        cef, pageInput, SendState, OnHidden);

    private void Start()
    {
        rpc.AdminChanged += Window.Deliver;
        rpc.AdminPartArrived += OnPart;
        rpc.AdminAnswered += OnAnswered;
        rpc.AdminClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.AdminChanged -= Window.Deliver;
        rpc.AdminPartArrived -= OnPart;
        rpc.AdminAnswered -= OnAnswered;
        rpc.AdminClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnPart(string kind, string json)
    {
        if (Window.ShownBrowser is { } browser)
            SendPart(browser, kind, json);
    }

    private void OnAnswered(string text, bool done)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, done);
    }

    // Closed however, by another page taking the input too: the server sends the answers to the chat again.
    private void OnHidden(BrowserHandle browser)
    {
        rpc.SendAdminClose();
        SendClosed(browser);
    }

    [ReceiveFromCef]
    private void OnAdminReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnAdminRun(BrowserHandle browser, string line) => rpc.SendAdminRun(line);

    [ReceiveFromCef]
    private void OnAdminAsk(BrowserHandle browser, string kind, string argument) => rpc.SendAdminAsk(kind, argument);

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnAdminClose(BrowserHandle browser) => Window.Close();

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendPart(BrowserHandle browser, string kind, string json);

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, string text, bool done);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
