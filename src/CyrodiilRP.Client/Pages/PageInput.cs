using ReadyM.Cef;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Pages;

// One page at a time holds the mouse and keyboard and takes the clicks over its screen. Every other page lets them
// through, so the chat's log or the party's list never stands in the way of the game's own windows.
[Service]
internal sealed partial class PageInput(CefHost cef)
{
    // The game takes the input back on its own, without a word, as a save loads or its pause menu closes.
    private const float HoldAgainSeconds = 1f;

    private WebPage? _holder;

    private float _lastHeld;

    private Action? _lost;

    public bool IsHeld => _holder is not null;

    public bool IsHeldBy(WebPage page) => _holder == page;

    // Exclusive by default, so a click beside a window keeps the keyboard in it.
    public void Take(WebPage page, Action lost, CefInputMode mode = CefInputMode.Exclusive)
    {
        if (_holder is { } previous && previous != page)
        {
            var closePrevious = _lost;
            _holder = null;
            _lost = null;
            cef.SetInputMode(previous.Viewport, CefInputMode.DisplayOnly);
            closePrevious?.Invoke();
        }

        _holder = page;
        _lost = lost;
        cef.SetInputMode(page.Viewport, mode);
        cef.SetVisible(page.Viewport, true);
        Hold(page);
    }

    public void Release(WebPage page)
    {
        if (_holder != page)
            return;

        _holder = null;
        _lost = null;
        cef.SetInputMode(page.Viewport, CefInputMode.DisplayOnly);
        cef.SetFocused(page.Viewport, false);
        cef.SetCursorVisible(false);
        cef.SetUiInputMode(false);
    }

    private void Update()
    {
        if (_holder is { } page && Time.Elapsed - _lastHeld >= HoldAgainSeconds)
            Hold(page);
    }

    private void Hold(WebPage page)
    {
        _lastHeld = Time.Elapsed;
        cef.SetUiInputMode(true);
        cef.SetCursorVisible(true);
        cef.SetFocused(page.Viewport, true);
        cef.SetPageFocused(page.Browser, true);
    }
}
