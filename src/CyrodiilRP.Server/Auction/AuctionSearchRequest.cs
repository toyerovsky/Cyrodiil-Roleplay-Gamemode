namespace CyrodiilRP.Server.Auction;

// As the page sent it. Category: an AuctionCategory by name.
internal sealed record AuctionSearchRequest(string Text, string Category, int Page);
