namespace CyrodiilRP.Data.Auction;

// Why a parcel waits at the auction house. The first five hold the lot's item, the rest gold.
public enum AuctionParcelReason
{
    Won,
    Bought,
    Unsold,
    Withdrawn,
    Removed,

    // The price of a lot sold, less the house's cut, for its seller.
    Sale,

    // A bid someone outbid, or one a purchase ended.
    Outbid,

    // A bid on a lot the staff took down.
    Refund,
}
