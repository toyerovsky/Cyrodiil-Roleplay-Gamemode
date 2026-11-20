using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Away;

// A key or the mouse in any of the mode's pages (ui/shared/presence.js): the player is at the keyboard while a window
// holds them still, which the server cannot see. It goes on to the server at most every half a minute, and while the AFK
// screen is up at once, so the screen goes at the first touch.
[Service]
internal sealed partial class PresenceReports(RpcHandlers rpc, AwayOverlay screen)
{
    // Well inside the minutes the server waits before it asks.
    private const long ReportEveryMs = 30_000;

    private const long ReportWhileShownMs = 1_000;

    private long? _reportedAtMs;

    [ReceiveFromCef]
    private void OnPresence(BrowserHandle browser)
    {
        var now = Environment.TickCount64;
        var every = screen.IsShown ? ReportWhileShownMs : ReportEveryMs;
        if (_reportedAtMs is { } last && now - last < every)
            return;

        _reportedAtMs = now;
        rpc.SendReportPresence();
    }
}
