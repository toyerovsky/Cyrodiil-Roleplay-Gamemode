using ReadyM.Cef;

namespace CyrodiilRP.Client.Pages;

// A window the server's state opens over the game: up, it holds the mouse and keyboard, and another page taking them
// hides it. What the server sends while the page is still loading waits for it.
internal sealed class PageWindow(
    OverlayPage page,
    CefHost cef,
    PageInput input,
    Action<BrowserHandle, string> sendState,
    Action<BrowserHandle> hidden,
    Action? lost = null)
{
    private bool _shown;

    private string? _pending;

    public bool IsShown => _shown;

    // Only while it is up, so nothing goes to a window the player does not see.
    public BrowserHandle? ShownBrowser => _shown && page.Opened is { } opened ? opened.Browser : null;

    public void Deliver(string state)
    {
        var opened = page.Open();
        if (!page.IsReady)
        {
            _pending = state;
            return;
        }

        Show(opened);
        sendState(opened.Browser, state);
    }

    public void MarkReady(BrowserHandle browser)
    {
        page.MarkReady(browser);
        if (_pending is not { } state || page.Opened is not { } opened)
            return;

        _pending = null;
        Show(opened);
        sendState(browser, state);
    }

    public void ApplyLocale(string current) => page.ApplyLocale(current);

    public void Close()
    {
        _pending = null;
        if (page.Opened is not { } opened)
            return;

        Hide(opened);
        input.Release(opened);
    }

    private void Show(WebPage opened)
    {
        if (_shown)
            return;

        _shown = true;
        input.Take(opened, lost: () =>
        {
            Hide(opened);
            lost?.Invoke();
        });
    }

    private void Hide(WebPage opened)
    {
        if (!_shown)
            return;

        _shown = false;
        hidden(opened.Browser);
        cef.SetVisible(opened.Viewport, false);
    }
}
