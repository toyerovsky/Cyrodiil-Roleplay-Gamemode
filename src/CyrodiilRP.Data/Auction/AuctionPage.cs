namespace CyrodiilRP.Data.Auction;

public sealed record AuctionPage(IReadOnlyList<AuctionLot> Lots, bool More);
