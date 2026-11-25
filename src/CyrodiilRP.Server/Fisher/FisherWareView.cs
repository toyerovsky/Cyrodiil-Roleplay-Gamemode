namespace CyrodiilRP.Server.Fisher;

// A ware of the fisher's shop. Ware: its key. Price: one piece's, in gold. ReachCm and Line: a rod's, null for a bait.
// Count: how many of a bait or the lure the player carries, null for a rod.
internal sealed record FisherWareView(
    string Ware,
    FisherWareKind Kind,
    string Icon,
    string Name,
    int Price,
    float Weight,
    float? ReachCm,
    double? Line,
    int? Count);
