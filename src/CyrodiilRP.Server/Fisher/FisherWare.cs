namespace CyrodiilRP.Server.Fisher;

// A ware of the fisher's shop, by its custom item's key. Icon: the page's painted one. ReachCm and Line: a rod's, null for
// a bait.
internal sealed record FisherWare(string Key, FisherWareKind Kind, string Icon, string Name, int Price, float Weight, float? ReachCm, double? Line);
