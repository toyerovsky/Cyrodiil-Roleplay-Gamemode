namespace CyrodiilRP.Data.Auction;

// Minimum: the least bid the lot takes now. Lot: as the bid left it, null when it is gone. OutbidCharacterId: whose bid
// it beat, whose OutbidGold now waits at the house.
public sealed record AuctionBidResult(AuctionBidOutcome Outcome, int Minimum, AuctionLot? Lot, long? OutbidCharacterId, int OutbidGold);
