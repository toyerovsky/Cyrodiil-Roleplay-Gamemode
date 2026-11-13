using Microsoft.Extensions.Logging;
using ReadyM.Cef;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Pages;

[Service]
internal sealed partial class WebPages(CefHost cef, PawnWatch pawn, ILogger logger)
{
    public const int ChatLayer = 0;

    public const int PartyLayer = 1;

    public const int FrozenLayer = 2;

    public const int AsksLayer = 3;

    public const int LoreLayer = 4;

    // Over the lore card, whose trait rolls land on the small board.
    public const int DiceLayer = 5;

    public const int WheelLayer = 6;

    public const int MenuLayer = 7;

    public const int TraderLayer = 8;

    // A Jobs foreman's window, beside the trader's: the two never open together.
    public const int ForemanLayer = TraderLayer;

    public const int BankLayer = 9;

    // Beside the bank's: a window at a thing in the world, and one of them open at a time.
    public const int BoardLayer = 9;

    public const int StableLayer = 10;

    public const int ExchangeLayer = 11;

    public const int AdminLayer = 12;

    public const int FormLayer = 13;

    // Over everything, the chat's log too.
    public const int WelcomeLayer = 14;

    // The game rebuilds the overlay CEF draws on after every load, and a page shown before it is there never comes up,
    // without a word. So a page that should be up is shown again this long after each new pawn.
    public const float OverlaySettleSeconds = 1f;

    private const string Host = "rpui";

    private readonly List<WebPage> _alwaysShown = [];

    // The pawn (PawnWatch.Number) the pages that stay up were last shown for.
    private int? _shownFor;

    // Only in Start: CEF freezes its configuration once the mods have started.
    private void Start()
    {
        var directory = Path.GetDirectoryName(typeof(WebPages).Assembly.Location)!;
        cef.RegisterFolder(Host, Path.Combine(directory, "web"));
    }

    // Full screen, so the page scales with the window. Clicks pass through it until it holds the input (PageInput).
    public WebPage Open(string page, int layer)
    {
        var viewport = cef.CreateViewport(zOrder: layer);
        cef.SetPlacement(viewport, ViewportPlacement.FullScreen);
        cef.SetInputMode(viewport, CefInputMode.DisplayOnly);

        var browser = cef.CreateBrowser();
        browser.Navigate(BuildUrl(page));
        cef.Assign(viewport, browser.Handle);

        logger.LogInformation("[CyrodiilRP] Page {Url} created.", BuildUrl(page));
        return new WebPage(viewport, browser.Handle);
    }

    public void ShowAlways(WebPage page)
    {
        _alwaysShown.Add(page);
        cef.SetVisible(page.Viewport, true);
    }

    private void Update()
    {
        if (_shownFor == pawn.Number || !pawn.HasSettled(OverlaySettleSeconds))
            return;

        _shownFor = pawn.Number;
        foreach (var page in _alwaysShown)
            cef.SetVisible(page.Viewport, true);
    }

    // The scheme serves index.html only at the root of a host, so a page is named in full.
    private static string BuildUrl(string page) => $"readym://{Host}/{page}/index.html";
}
