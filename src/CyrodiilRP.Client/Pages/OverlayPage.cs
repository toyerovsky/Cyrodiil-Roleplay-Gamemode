using ReadyM.Cef;

namespace CyrodiilRP.Client.Pages;

// The page an overlay owns: made the first time it is needed, ready once it says it has loaded (again after every
// reload), and in the player's language.
internal sealed class OverlayPage(
    WebPages pages,
    PageLocale locale,
    string name,
    int layer,
    Action<BrowserHandle, string> sendLocale)
{
    public WebPage? Opened { get; private set; }

    // A message sent before the page has loaded is dropped.
    public bool IsReady { get; private set; }

    public WebPage Open() => Opened ??= pages.Open(name, layer);

    public void MarkReady(BrowserHandle browser)
    {
        IsReady = true;
        sendLocale(browser, locale.Current);
    }

    public void ApplyLocale(string current)
    {
        if (Opened is { } page)
            sendLocale(page.Browser, current);
    }
}
