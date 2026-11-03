using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Creator;

[Service]
internal sealed partial class NameFormOverlay(
    CefHost cef,
    WebPages pages,
    PageInput input,
    RpcHandlers rpc,
    PageLocale locale,
    PawnWatch pawn)
{
    private const string PageName = "creator";

    private OverlayPage? _page;

    private bool _naming;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.FormLayer, SendLocale);

    private void Start()
    {
        locale.Changed += Page.ApplyLocale;
        rpc.NamingStarted += OnNamingStarted;
        rpc.NameAnswered += OnNameAnswered;
        rpc.AppearanceStarted += OnAppearanceStarted;
    }

    private void Stop()
    {
        locale.Changed -= Page.ApplyLocale;
        rpc.NamingStarted -= OnNamingStarted;
        rpc.NameAnswered -= OnNameAnswered;
        rpc.AppearanceStarted -= OnAppearanceStarted;
        HideForm();
    }

    // The form waits for the character to be in the world, which Update watches.
    private void OnNamingStarted()
    {
        _naming = true;
        Page.Open();
    }

    // Empty when the name was accepted: the server asks for the appearance next, which hides the form.
    private void OnNameAnswered(string problem)
    {
        if (Page.Opened is { } page)
            SendNameAnswer(page.Browser, problem);
    }

    private void OnAppearanceStarted()
    {
        _naming = false;
        HideForm();
    }

    // Without a character in the world there is no cursor, and a save loading takes the world away, so the form holds the
    // keyboard only while there is one, taken from any other page: the server waits for it. Never before the page is
    // there, so input never goes to a page that is not on screen.
    private void Update()
    {
        if (!_naming || Page.Opened is not { } page || !Page.IsReady)
            return;

        var held = input.IsHeldBy(page);
        if (pawn.InWorld && !held)
            TakeInput(page);
        else if (!pawn.InWorld && held)
            HideForm();
    }

    // A reloaded page starts hidden, so the form is shown again whenever the page says it is there.
    [ReceiveFromCef]
    private void OnCreatorReady(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        if (Page.Opened is { } page && input.IsHeldBy(page))
            TakeInput(page);
    }

    [ReceiveFromCef]
    private void OnSubmitName(BrowserHandle browser, string firstName, string lastName) => rpc.SendSubmitName(firstName, lastName);

    private void TakeInput(WebPage page)
    {
        input.Take(page, lost: () => Hide(page));
        SendCreatorShown(page.Browser, true);
    }

    private void HideForm()
    {
        if (Page.Opened is not { } page)
            return;

        Hide(page);
        input.Release(page);
    }

    private void Hide(WebPage page)
    {
        SendCreatorShown(page.Browser, false);
        cef.SetVisible(page.Viewport, false);
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendCreatorShown(BrowserHandle browser, bool shown);

    [SendToCef]
    private partial void SendNameAnswer(BrowserHandle browser, string problem);
}
