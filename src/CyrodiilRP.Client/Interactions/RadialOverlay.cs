using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Pages;
using OblivionMP.SDK;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;
using OblivionMpCSharpMod.Input;

namespace CyrodiilRP.Client.Interactions;

[Service]
internal sealed partial class RadialOverlay(
    CefHost cef,
    WebPages pages,
    PageInput pageInput,
    RpcHandlers rpc,
    PageLocale locale,
    IInput input,
    IUserInterface ui,
    ChatOverlay chat)
{
    private const string PageName = "radial";

    // As the page names them in a keydown, so the key that opened a wheel also closes it.
    private const string SelfKeyName = "g";
    private const string InteractionKeyName = "h";

    private PageWindow? _window;

    private string _openedBy = string.Empty;

    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.WheelLayer, SendLocale),
        cef, pageInput, (browser, json) => SendWheel(browser, json, _openedBy), SendHidden, lost: rpc.SendCloseWheel);

    private void Start()
    {
        rpc.WheelShown += OnWheelShown;
        locale.Changed += Window.ApplyLocale;

        // Polled, as the chat's K: keys the game itself leaves alone, since it sees them too (F1 to F8 are its menus).
        input.RegisterKeyBind(Key.G, () => Ask(SelfKeyName, rpc.SendOpenSelfWheel), KeySource.Polled);
        input.RegisterKeyBind(Key.H, () => Ask(InteractionKeyName, rpc.SendOpenInteractionWheel), KeySource.Polled);
    }

    private void Stop()
    {
        rpc.WheelShown -= OnWheelShown;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    // The chat too: an h typed in it reaches the bind.
    private void Ask(string keyName, Action ask)
    {
        if (pageInput.IsHeld || chat.IsOpen || ui.IsCharacterCreatorOpen())
            return;

        _openedBy = keyName;
        ask();
    }

    // The server keeps one wheel a player, the last it sent, so a wheel that comes while this one is up takes its
    // place. One that comes while another page holds the input lost the race to that page, which came up after the
    // press: it is closed unseen, here and on the server.
    private void OnWheelShown(string json)
    {
        if (pageInput.IsHeld && !Window.IsShown)
        {
            Window.Close();
            rpc.SendCloseWheel();
            return;
        }

        Window.Deliver(json);
    }

    [ReceiveFromCef]
    private void OnWheelReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnWheelPick(BrowserHandle browser, string id)
    {
        Window.Close();
        rpc.SendPickWheel(id);
    }

    [ReceiveFromCef]
    private void OnWheelClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendCloseWheel();
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendWheel(BrowserHandle browser, string json, string key);

    [SendToCef]
    private partial void SendHidden(BrowserHandle browser);
}
