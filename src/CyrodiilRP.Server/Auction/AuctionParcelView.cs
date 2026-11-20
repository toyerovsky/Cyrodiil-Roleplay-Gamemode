using CyrodiilRP.Data.Auction;

namespace CyrodiilRP.Server.Auction;

// What waits for the reader. Gold: 0 for one that holds the item of its lot (Name, Count, Used).
internal sealed record AuctionParcelView(long Id, AuctionParcelReason Reason, int Gold, string Name, int Count, bool Used);
