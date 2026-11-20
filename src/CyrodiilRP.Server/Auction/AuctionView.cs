namespace CyrodiilRP.Server.Auction;

// The auction house's window, sent as it opens and again as the lots or what the character carries change. Gold and
// Items: what the character carries to pay with and to put up.
internal sealed record AuctionView(
    int Gold,
    AuctionTermsView Terms,
    IReadOnlyList<AuctionItemView> Items,
    AuctionPageView Browse,
    IReadOnlyList<AuctionLotView> Selling,
    IReadOnlyList<AuctionLotView> Bidding,
    IReadOnlyList<AuctionParcelView> Parcels);
