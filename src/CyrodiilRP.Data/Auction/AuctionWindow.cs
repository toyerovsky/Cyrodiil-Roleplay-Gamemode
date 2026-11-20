namespace CyrodiilRP.Data.Auction;

// What the auction house's window shows a character. Selling: their open lots. Bidding: the open lots they hold the top
// bid on.
public sealed record AuctionWindow(AuctionPage Page, IReadOnlyList<AuctionLot> Selling, IReadOnlyList<AuctionLot> Bidding, IReadOnlyList<PendingParcel> Parcels);
