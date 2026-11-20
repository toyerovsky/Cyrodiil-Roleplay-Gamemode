namespace CyrodiilRP.Data.Auction;

// The house's rules a bid is held to. StepPercent: how far over the top bid the next must go. Extension: how near the
// end a bid moves it.
public sealed record AuctionBidTerms(DateTimeOffset Now, int StepPercent, TimeSpan Extension);
