using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Pages;
using OblivionMP.SDK;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;
using OblivionMpCSharpMod.Input;

namespace CyrodiilRP.Client.Lore;

// The lore card: the player's own to fill in, or another player's to read. The page asks, the server answers; this class
// carries the texts and shows or hides the window. End opens or closes the player's own card, Shift+End the card of the
// player in front.
[Service]
internal sealed partial class LoreOverlay(
    CefHost cef,
    WebPages pages,
    PageInput pageInput,
    RpcHandlers rpc,
    PageLocale locale,
    IInput input,
    IUserInterface ui,
    PawnWatch pawn,
    ChatOverlay chat)
{
    private const string PageName = "lore";

    // The server's own message, which opens the window.
    private const int PushId = 0;

    // Shift+End also fires End's bind: End waits this long to see whether it was a look at someone.
    private const float EndWaitSeconds = 0.05f;
    private const float LookWindowSeconds = 0.25f;

    private PageWindow? _window;

    private float _endAt = -1f;

    private float _lookAt = float.NegativeInfinity;

    // A box of the page has the keyboard: End moves its caret then.
    private bool _typing;

    // Another page taking the input hides the card: the server hears it is closed.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.LoreLayer, SendLocale),
        cef, pageInput, (browser, json) => SendAnswer(browser, PushId, json), Hidden, lost: rpc.SendCloseLore);

    private void Start()
    {
        rpc.LoreAnswered += OnAnswered;
        rpc.LoreCardClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;

        // Polled, as the mode's other keys. The Remaster leaves End alone.
        input.RegisterKeyBind(Key.END, () => _endAt = Time.Elapsed, KeySource.Polled);
        input.RegisterKeyBind(ModifierKeys.Shift, Key.END, OnLook, KeySource.Polled);
    }

    private void Stop()
    {
        rpc.LoreAnswered -= OnAnswered;
        rpc.LoreCardClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void Update()
    {
        if (_endAt < 0f || Time.Elapsed - _endAt < EndWaitSeconds)
            return;

        var looked = _lookAt >= _endAt - LookWindowSeconds;
        _endAt = -1f;
        if (!looked)
            OnEnd();
    }

    private void OnEnd()
    {
        if (Window.IsShown)
        {
            if (!_typing)
                CloseCard();

            return;
        }

        if (MayOpen())
            rpc.SendOpenLore(false);
    }

    private void OnLook()
    {
        _lookAt = Time.Elapsed;
        if (!Window.IsShown && MayOpen())
            rpc.SendOpenLore(true);
    }

    private bool MayOpen() => !pageInput.IsHeld && !chat.IsOpen && !ui.IsCharacterCreatorOpen() && pawn.InWorld;

    private void CloseCard()
    {
        Window.Close();
        rpc.SendCloseLore();
    }

    private void Hidden(BrowserHandle browser)
    {
        _typing = false;
        SendClosed(browser);
    }

    // The server's own message opens the window, an answer goes to the window while it is up.
    private void OnAnswered(int requestId, string json)
    {
        if (requestId == PushId)
            Window.Deliver(json);
        else if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, requestId, json);
    }

    [ReceiveFromCef]
    private void OnLoreReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnLoreAsk(BrowserHandle browser, int requestId, string json) => rpc.SendLoreAsk(requestId, json);

    // Esc or the close button on the page. End is left to the bind alone, which closes the window itself.
    [ReceiveFromCef]
    private void OnLoreClose(BrowserHandle browser) => CloseCard();

    [ReceiveFromCef]
    private void OnLoreTyping(BrowserHandle browser, bool typing) => _typing = typing && Window.IsShown;

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, int requestId, string json);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
