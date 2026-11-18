using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Specializations;

[Service]
internal sealed partial class MentorOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "mentor";

    private PageWindow? _window;

    // Another page taking the input closes the window, so a specialization is never chosen unseen.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.MentorLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: rpc.SendMentorClose);

    private void Start()
    {
        rpc.MentorChanged += Window.Deliver;
        rpc.MentorAnswered += OnAnswered;
        rpc.MentorClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.MentorChanged -= Window.Deliver;
        rpc.MentorAnswered -= OnAnswered;
        rpc.MentorClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnAnswered(string text, bool chosen)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, chosen);
    }

    [ReceiveFromCef]
    private void OnMentorReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnMentorChoose(BrowserHandle browser, string specialization) => rpc.SendMentorChoose(specialization);

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnMentorClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendMentorClose();
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, string text, bool chosen);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
