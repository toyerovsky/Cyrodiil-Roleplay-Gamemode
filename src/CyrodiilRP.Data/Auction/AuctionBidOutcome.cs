namespace CyrodiilRP.Data.Auction;

public enum AuctionBidOutcome
{
    Accepted,

    // Sold, ended or taken back since the window showed it.
    Gone,

    OwnLot,

    // Only bought now.
    NoBidding,

    // The bidder holds the top bid already.
    Leading,

    TooLow,

    // At the price to buy it now, which buys it instead.
    AtBuyout,
}
