namespace CyrodiilRP.Data.Auction;

public enum AuctionBuyOutcome
{
    Bought,

    // Sold, ended or taken back since the window showed it.
    Gone,

    OwnLot,

    // Only bid on.
    NoBuyout,

    // Not the price the window showed.
    PriceChanged,
}
