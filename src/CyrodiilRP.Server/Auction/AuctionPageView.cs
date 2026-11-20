namespace CyrodiilRP.Server.Auction;

// One page of the open lots the search found, the soonest to end first.
internal sealed record AuctionPageView(string Text, AuctionCategory Category, int Page, bool More, IReadOnlyList<AuctionLotView> Lots);
