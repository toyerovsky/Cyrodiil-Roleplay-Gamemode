namespace CyrodiilRP.Server.Fisher;

// The fisher's window, sent as it opens and again as the gold or the fishing things the player carries change. Most: the
// most pieces one deal buys or sells.
internal sealed record FisherView(
    int Gold,
    int Most,
    IReadOnlyList<FisherCatchView> Catch,
    IReadOnlyList<FisherWareView> Shop,
    IReadOnlyList<FisherRepairView> Repairs);
