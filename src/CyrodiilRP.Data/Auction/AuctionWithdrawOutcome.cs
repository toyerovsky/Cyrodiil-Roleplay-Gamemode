namespace CyrodiilRP.Data.Auction;

public enum AuctionWithdrawOutcome
{
    Withdrawn,

    // Sold, ended or taken back already.
    Gone,

    NotYours,

    // Someone bid on it, so it runs to its end.
    HasBids,
}
