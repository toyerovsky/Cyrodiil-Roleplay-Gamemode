using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Pages;
using OblivionMp.Sdk;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;
using ReadyM.Sdk.Common.Input;

namespace CyrodiilRP.Client.PlayerMenu;

[Service]
internal sealed partial class PlayerMenuOverlay(
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
    private const string PageName = "menu";

    private PageWindow? _window;

    // A page that takes the input closes the menu: /help from the chat takes it from the chat in turn.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.MenuLayer, SendLocale),
        cef, pageInput, SendState, SendClosed);

    private void Start()
    {
        rpc.MenuChanged += Window.Deliver;
        rpc.FactionAnswered += OnFactionAnswered;
        locale.Changed += Window.ApplyLocale;

        // Polled, as the other keys. The Remaster leaves F10 alone: F1 to F8 are its menus and F9 its quick load.
        input.RegisterKeyBind(Key.F10, OnKey, KeySource.Polled);
    }

    private void Stop()
    {
        rpc.MenuChanged -= Window.Deliver;
        rpc.FactionAnswered -= OnFactionAnswered;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnKey()
    {
        if (Window.IsShown)
        {
            Window.Close();
            return;
        }

        if (pageInput.IsHeld || chat.IsOpen || ui.IsCharacterCreatorOpen() || !pawn.InWorld)
            return;

        rpc.SendOpenMenu();
    }

    // Onto the faction tab that asked, with the menu as it is now. Closed since, the menu stays closed and the answer
    // shows in the corner.
    private void OnFactionAnswered(string text, bool done, string menu)
    {
        if (Window.ShownBrowser is not { } browser)
        {
            rpc.ShowNotice(text);
            return;
        }

        SendAnswer(browser, text, done);
        SendState(browser, menu);
    }

    [ReceiveFromCef]
    private void OnMenuReady(BrowserHandle browser) => Window.MarkReady(browser);

    // Esc or the close button on the page. F10 is left to OnKey alone: the page's keydown comes first, and the polled
    // press after it would find the menu closed and open it again.
    [ReceiveFromCef]
    private void OnMenuClose(BrowserHandle browser) => Window.Close();

    [ReceiveFromCef]
    private void OnMenuLanguage(BrowserHandle browser, string language) => rpc.SendChooseLanguage(language);

    // The dice tab's button: the board takes the menu's place.
    [ReceiveFromCef]
    private void OnMenuDice(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendOpenDice();
    }

    [ReceiveFromCef]
    private void OnMenuDiceSet(BrowserHandle browser, string set) => rpc.SendChooseDiceSet(set, true);

    // The lore tab's button: the player's own card takes the menu's place.
    [ReceiveFromCef]
    private void OnMenuLore(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendOpenLore(false);
    }

    [ReceiveFromCef]
    private void OnMenuLoreName(BrowserHandle browser, bool namePublic) => rpc.SendChooseNamePublic(namePublic);

    // A button of the faction tab: a /faction command, which the server runs as if typed.
    [ReceiveFromCef]
    private void OnMenuFaction(BrowserHandle browser, string line) => rpc.SendFactionRun(line);

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, string text, bool done);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
