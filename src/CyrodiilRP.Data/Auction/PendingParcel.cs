namespace CyrodiilRP.Data.Auction;

// A parcel not collected yet, with the item of its lot. Gold: 0 for one that holds the item. CustomItem: as the lot's.
public sealed record PendingParcel(long Id, AuctionParcelReason Reason, int Gold, long LotId, string ItemName, long Form, string? CustomItem, int Count,
    float Health, float Charge);
