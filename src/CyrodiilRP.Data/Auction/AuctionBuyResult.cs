namespace CyrodiilRP.Data.Auction;

// Lot: as the purchase left it, null when it is gone. OutbidCharacterId: whose top bid the purchase ended, whose
// OutbidGold now waits at the house.
public sealed record AuctionBuyResult(AuctionBuyOutcome Outcome, AuctionLot? Lot, long? OutbidCharacterId, int OutbidGold);
