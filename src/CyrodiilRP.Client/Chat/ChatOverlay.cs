using System.Text.Json;
using CyrodiilRP.Client.Pages;
using CyrodiilRP.Common.Chat;
using OblivionMp.Sdk;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;
using ReadyM.Sdk.Common.Input;

namespace CyrodiilRP.Client.Chat;

// Replaces the platform's chat, which has to be off the server, since both answer K. Every page shares one registry, so
// the names of the messages from a page are unique across pages.
[Service]
internal sealed partial class ChatOverlay(
    CefHost cef,
    WebPages pages,
    PageInput pageInput,
    RpcHandlers rpc,
    PageLocale locale,
    IInput input,
    IUserInterface ui)
{
    private const string PageName = "chat";

    private readonly List<PageLine> _lines = new(ChatLimits.HistoryLines);

    private OverlayPage? _page;

    private bool _open;

    // The start of a command the wheel put in, until it is sent or the chat closes.
    private string _draft = string.Empty;

    public bool IsOpen => _open;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.ChatLayer, SendLocale);

    private void Start()
    {
        rpc.MessageDelivered += Add;
        rpc.ChatPrefilled += OpenWith;
        locale.Changed += Page.ApplyLocale;

        // Polled: in play the game's viewport takes every key, so an unhandled press never comes. The game sees the K
        // too, and the page leaves out the K that opened it.
        input.RegisterKeyBind(Key.K, Open, KeySource.Polled);
    }

    private void Stop()
    {
        rpc.MessageDelivered -= Add;
        rpc.ChatPrefilled -= OpenWith;
        locale.Changed -= Page.ApplyLocale;
    }

    private void OpenWith(string text)
    {
        if (pageInput.IsHeld)
            return;

        _draft = text;
        Open();
        SendDraft(EnsurePage().Browser, text);
    }

    // Not over the game's own creator, which would lose its cursor when the chat gives the mouse back.
    private void Open()
    {
        if (pageInput.IsHeld || _open || ui.IsCharacterCreatorOpen())
            return;

        // The mouse too, so the cursor shows and the wheel scrolls the log. Bubbling: a click beside the chat goes to the
        // game, which takes the focus from the page, and that closes it.
        pageInput.Take(EnsurePage(), lost: () => SetOpen(false), CefInputMode.Bubbling);
        SetOpen(true);
    }

    private void SetOpen(bool open)
    {
        if (_open == open)
            return;

        _open = open;
        if (!open)
            _draft = string.Empty;

        if (Page.Opened is { } page)
            SendOpen(page.Browser, open);
    }

    private void Add(ChatLine line)
    {
        if (_lines.Count == ChatLimits.HistoryLines)
            _lines.RemoveAt(0);

        var entry = new PageLine(line.Kind.ToString(), line.Text ?? string.Empty, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _lines.Add(entry);

        // The first line creates the page, which then asks for the history with this line in it.
        SendLine(EnsurePage().Browser, ToJson([entry]));
    }

    // Esc on the page.
    [ReceiveFromCef]
    private void OnClose(BrowserHandle browser) => Close();

    // A click beside the chat, into the game. When a form took the keyboard instead, the form holds it now and the
    // release changes nothing.
    [ReceiveFromCef]
    private void OnBlurred(BrowserHandle browser) => Close();

    private void Close()
    {
        if (!_open)
            return;

        SetOpen(false);
        pageInput.Release(EnsurePage());
    }

    [ReceiveFromCef]
    private void OnSubmit(BrowserHandle browser, string text)
    {
        _draft = string.Empty;
        rpc.SendSubmitMessage(text);
    }

    // The first thing the page does, so it is also how the game hears that one has loaded.
    [ReceiveFromCef]
    private void OnRequestHistory(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        SendHistory(browser, ToJson(_lines));
        SendOpen(browser, _open);
        if (_draft.Length > 0)
            SendDraft(browser, _draft);

        // A new document starts unfocused.
        if (_open)
            cef.SetPageFocused(browser, true);
    }

    private WebPage EnsurePage()
    {
        if (Page.Opened is { } page)
            return page;

        page = Page.Open();
        pages.ShowAlways(page);
        return page;
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendLine(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendHistory(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendOpen(BrowserHandle browser, bool open);

    [SendToCef]
    private partial void SendDraft(BrowserHandle browser, string text);

    private static string ToJson(List<PageLine> lines) => JsonSerializer.Serialize(lines, PageJson.Default.ListPageLine);
}
