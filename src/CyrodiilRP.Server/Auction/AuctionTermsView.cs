namespace CyrodiilRP.Server.Auction;

// The house's rules from the config, so the window checks a lot as the server will.
internal sealed record AuctionTermsView(int FeePercent, int CutPercent, IReadOnlyList<int> Hours, int MaxLots, int MaxPrice);
