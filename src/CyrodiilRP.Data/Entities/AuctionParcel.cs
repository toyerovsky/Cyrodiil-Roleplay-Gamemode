using CyrodiilRP.Data.Auction;

namespace CyrodiilRP.Data;

// What waits for a character at any auctioneer: the item of its lot, or Gold. CollectedAt: when it was handed over.
public sealed class AuctionParcel
{
    public long Id { get; set; }

    public long CharacterId { get; set; }

    public long LotId { get; set; }

    public AuctionParcelReason Reason { get; set; }

    public int Gold { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CollectedAt { get; set; }
}
