using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Gambling;

[Service]
internal sealed partial class GamblingOverlay(CefHost cef, WebPages pages, PageInput input, RpcHandlers rpc, PageLocale locale)
{
    private PageWindow? _window;
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, "gambling", WebPages.GamblingLayer, SendLocale),
        cef, input, SendState, SendClosed, lost: rpc.SendGamblingClose);

    private void Start()
    {
        rpc.GamblingChanged += Window.Deliver;
        rpc.GamblingInvitationShown += Window.Close;
        rpc.GamblingRefused += Refuse;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.GamblingChanged -= Window.Deliver;
        rpc.GamblingInvitationShown -= Window.Close;
        rpc.GamblingRefused -= Refuse;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void Refuse(string text)
    {
        if (Window.ShownBrowser is { } browser) SendRefused(browser, text);
        else rpc.ShowNotice(text);
    }

    [ReceiveFromCef]
    private void OnGamblingReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnGamblingClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendGamblingClose();
    }

    [ReceiveFromCef]
    private void OnGamblingInvite(BrowserHandle browser, int opponent, int stake, string mode) => rpc.SendGamblingInvite(opponent, stake, mode);

    [ReceiveFromCef]
    private void OnGamblingAct(BrowserHandle browser, long game, int version, string action, int amount) => rpc.SendGamblingAct(game, version, action, amount);

    [ReceiveFromCef]
    private void OnGamblingRefresh(BrowserHandle browser) => rpc.SendGamblingOpen(-1, false);

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendRefused(BrowserHandle browser, string text);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
