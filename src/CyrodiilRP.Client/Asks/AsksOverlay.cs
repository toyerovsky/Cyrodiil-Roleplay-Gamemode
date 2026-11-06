using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Pages;
using OblivionMP.SDK;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;
using OblivionMpCSharpMod.Input;

namespace CyrodiilRP.Client.Asks;

// The invitations to a party or a trade, only to be read, as the party's panel: Y and N answer the one the page marks,
// wherever the player stands, so nobody has to find the asker with the wheel.
[Service]
internal sealed partial class AsksOverlay(
    WebPages pages,
    PageInput pageInput,
    RpcHandlers rpc,
    PageLocale locale,
    IInput input,
    IUserInterface ui,
    ChatOverlay chat)
{
    private const string PageName = "asks";
    private const string NoAsks = """{"asks":[],"answerable":0}""";
    private const int NoneAnswerable = 0;

    private OverlayPage? _page;

    // The last state, which a page loaded anew asks for.
    private string _state = NoAsks;

    private int _answerable = NoneAnswerable;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.AsksLayer, SendLocale);

    private void Start()
    {
        rpc.AsksChanged += OnChanged;
        locale.Changed += Page.ApplyLocale;

        // Polled, as the wheels' G and H: keys the game leaves alone.
        input.RegisterKeyBind(Key.Y, () => Answer(accepted: true), KeySource.Polled);
        input.RegisterKeyBind(Key.N, () => Answer(accepted: false), KeySource.Polled);
    }

    private void Stop()
    {
        rpc.AsksChanged -= OnChanged;
        locale.Changed -= Page.ApplyLocale;
    }

    // A letter typed into a page, the chat among them, reaches the bind too.
    private void Answer(bool accepted)
    {
        if (_answerable == NoneAnswerable || pageInput.IsHeld || chat.IsOpen || ui.IsCharacterCreatorOpen())
            return;

        rpc.SendAnswerAsk(_answerable, accepted);
    }

    private void OnChanged(string json, int answerable)
    {
        _state = json;
        _answerable = answerable;
        if (Page.Opened is not { } page)
        {
            page = Page.Open();
            pages.ShowAlways(page);
        }

        if (Page.IsReady)
            SendState(page.Browser, json);
    }

    [ReceiveFromCef]
    private void OnAsksReady(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        SendState(browser, _state);
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);
}
