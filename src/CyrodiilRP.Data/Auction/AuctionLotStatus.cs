namespace CyrodiilRP.Data.Auction;

public enum AuctionLotStatus
{
    Open,

    // Bought now, or won by the top bid when its time ran out.
    Sold,

    // Its time ran out without a bid.
    Expired,

    // The seller took it back before anyone bid.
    Withdrawn,

    // The staff took it down.
    Removed,
}
