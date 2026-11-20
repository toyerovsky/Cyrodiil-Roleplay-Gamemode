namespace CyrodiilRP.Server.Auction;

// A lot as the reader sees it, without its seller. MinimumBid: 0 for a lot only bought now. EndsInMs: from when it was
// sent. Mine: the reader put it up. Leading: the reader holds its top bid.
internal sealed record AuctionLotView(
    long Id,
    string Name,
    int Count,
    bool Used,
    AuctionCategory Category,
    int? StartPrice,
    int? BuyoutPrice,
    int? TopBid,
    int Bids,
    int MinimumBid,
    long EndsInMs,
    bool Mine,
    bool Leading);
