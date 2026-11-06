using System.Text.Json;
using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Pages;
using OblivionMP.SDK;
using OblivionMpCSharpMod;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;
using OblivionMpCSharpMod.Input;

namespace CyrodiilRP.Client.Dice;

// The dice board: a window the server opens (Home, the wheel, the menu, /board), and the same page small and out of the
// way when the server shows a roll made for something else. The numbers always come from the server.
[Service]
internal sealed partial class DiceOverlay(
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
    private const string PageName = "dice";

    // As long as the game's own notices stay.
    private const float RefusalSeconds = 5f;

    private OverlayPage? _page;

    private PageWindow? _window;

    // A roll to show before the page has loaded.
    private Action<BrowserHandle>? _pendingPresent;

    private bool _presenting;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.DiceLayer, SendLocale);

    // Another page taking the input hides the board: the server hears it is closed.
    private PageWindow Window => _window ??= new PageWindow(Page, cef, pageInput, SendState, SendClosed, lost: rpc.SendCloseDice);

    private void Start()
    {
        rpc.DiceChanged += Window.Deliver;
        rpc.DiceBoardClosed += Window.Close;
        rpc.DiceThrown += OnThrown;
        rpc.DiceRefused += OnRefused;
        rpc.DiceThrownOnBoard += OnThrownOnBoard;
        rpc.DicePresented += OnPresented;
        locale.Changed += Window.ApplyLocale;

        // Polled, as the mode's other keys. The Remaster leaves Home alone.
        input.RegisterKeyBind(Key.HOME, OnKey, KeySource.Polled);
    }

    private void Stop()
    {
        rpc.DiceChanged -= Window.Deliver;
        rpc.DiceBoardClosed -= Window.Close;
        rpc.DiceThrown -= OnThrown;
        rpc.DiceRefused -= OnRefused;
        rpc.DiceThrownOnBoard -= OnThrownOnBoard;
        rpc.DicePresented -= OnPresented;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnKey()
    {
        if (Window.IsShown)
        {
            CloseBoard();
            return;
        }

        if (pageInput.IsHeld || chat.IsOpen || ui.IsCharacterCreatorOpen() || !pawn.InWorld)
            return;

        rpc.SendOpenDice();
    }

    private void CloseBoard()
    {
        Window.Close();
        rpc.SendCloseDice();
    }

    private void OnThrown(int requestId, int[] values)
    {
        if (Window.ShownBrowser is { } browser)
            SendThrown(browser, requestId, Serialize(values));
    }

    // On the board, or in the game's own line when the board has closed meanwhile.
    private void OnRefused(int requestId, string text)
    {
        if (Window.ShownBrowser is { } browser)
            SendRefused(browser, requestId, text);
        else
            ui.ShowMessage(text, MessagePosition.TopLeft, RefusalSeconds);
    }

    private void OnThrownOnBoard(int d6, int d10, int d20, int d100)
    {
        if (Window.ShownBrowser is { } browser)
            SendThrowOnBoard(browser, d6, d10, d20, d100);
    }

    private void OnPresented(string reason, int d6, int d10, int d20, int d100, int[] values)
    {
        var json = Serialize(values);
        if (Window.ShownBrowser is { } shown)
        {
            SendPresent(shown, reason, d6, d10, d20, d100, json);
            return;
        }

        var page = Page.Open();
        void Present(BrowserHandle browser)
        {
            _presenting = true;
            cef.SetVisible(page.Viewport, true);
            SendPresent(browser, reason, d6, d10, d20, d100, json);
        }

        if (Page.IsReady)
            Present(page.Browser);
        else
            _pendingPresent = Present;
    }

    private static string Serialize(int[] values) => JsonSerializer.Serialize(values, DiceClientJson.Default.Int32Array);

    [ReceiveFromCef]
    private void OnDiceReady(BrowserHandle browser)
    {
        Window.MarkReady(browser);
        if (_pendingPresent is { } present)
        {
            _pendingPresent = null;
            present(browser);
        }
    }

    [ReceiveFromCef]
    private void OnDiceClose(BrowserHandle browser) => CloseBoard();

    [ReceiveFromCef]
    private void OnDiceThrow(BrowserHandle browser, int requestId, int d6, int d10, int d20, int d100) =>
        rpc.SendThrowDice(requestId, d6, d10, d20, d100);

    [ReceiveFromCef]
    private void OnDiceSet(BrowserHandle browser, string set) => rpc.SendChooseDiceSet(set, false);

    // The small board has played out: gone, unless the window opened over it meanwhile.
    [ReceiveFromCef]
    private void OnDicePresented(BrowserHandle browser)
    {
        if (!_presenting)
            return;

        _presenting = false;
        if (!Window.IsShown && Page.Opened is { } page)
            cef.SetVisible(page.Viewport, false);
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);

    [SendToCef]
    private partial void SendThrown(BrowserHandle browser, int requestId, string valuesJson);

    [SendToCef]
    private partial void SendRefused(BrowserHandle browser, int requestId, string text);

    [SendToCef]
    private partial void SendThrowOnBoard(BrowserHandle browser, int d6, int d10, int d20, int d100);

    [SendToCef]
    private partial void SendPresent(BrowserHandle browser, string reason, int d6, int d10, int d20, int d100, string valuesJson);
}
