using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Party;

// Only to be read: the panel never takes the mouse or the keyboard.
[Service]
internal sealed partial class PartyOverlay(WebPages pages, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "party";
    private const string NoParty = """{"members":[]}""";

    private OverlayPage? _page;

    // The last list, which a page loaded anew asks for.
    private string _state = NoParty;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.PartyLayer, SendLocale);

    private void Start()
    {
        rpc.PartyChanged += OnChanged;
        locale.Changed += Page.ApplyLocale;
    }

    private void Stop()
    {
        rpc.PartyChanged -= OnChanged;
        locale.Changed -= Page.ApplyLocale;
    }

    private void OnChanged(string json)
    {
        _state = json;
        if (Page.Opened is not { } page)
        {
            page = Page.Open();
            pages.ShowAlways(page);
        }

        if (Page.IsReady)
            SendState(page.Browser, json);
    }

    [ReceiveFromCef]
    private void OnPartyReady(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        SendState(browser, _state);
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);
}
