using CyrodiilRP.Data.Auction;

namespace CyrodiilRP.Data;

// Copies of one item a character put up, held by the house until they are sold, run out or are taken back. Form, Health
// and Charge: the stack as the game keeps it. ItemName and ItemType: the catalogue's, for the search. SellerName: as it
// was, for the staff.
public sealed class AuctionLot
{
    public long Id { get; set; }

    public long SellerCharacterId { get; set; }

    public required string SellerName { get; set; }

    public long Form { get; set; }

    public required string ItemName { get; set; }

    public required string ItemType { get; set; }

    public int Count { get; set; }

    public float Health { get; set; }

    public float Charge { get; set; }

    // Null for a lot that is only bought now.
    public int? StartPrice { get; set; }

    // Null for a lot that is only bid on.
    public int? BuyoutPrice { get; set; }

    public int Fee { get; set; }

    public int? TopBid { get; set; }

    public long? TopBidderCharacterId { get; set; }

    public int Bids { get; set; }

    public AuctionLotStatus Status { get; set; }

    public long? BuyerCharacterId { get; set; }

    public int? Price { get; set; }

    public int Cut { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
}
