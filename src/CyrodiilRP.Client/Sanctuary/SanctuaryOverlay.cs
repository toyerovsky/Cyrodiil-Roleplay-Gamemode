using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Sanctuary;

// The Sanctuary's badge: only to be read, it never takes the mouse or the keyboard.
[Service]
internal sealed partial class SanctuaryOverlay(WebPages pages, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "sanctuary";
    private const string Outside = """{"inside":false,"aggressor":false,"secondsLeft":0}""";

    private OverlayPage? _page;

    // The last state, which a page loaded anew asks for.
    private string _state = Outside;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.SanctuaryLayer, SendLocale);

    private void Start()
    {
        rpc.SanctuaryChanged += OnChanged;
        locale.Changed += Page.ApplyLocale;
    }

    private void Stop()
    {
        rpc.SanctuaryChanged -= OnChanged;
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
    private void OnSanctuaryReady(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        SendState(browser, _state);
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);
}
