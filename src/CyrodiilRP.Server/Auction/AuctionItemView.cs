namespace CyrodiilRP.Server.Auction;

// A stack the character carries and may put up. Key: what the page names it by. Used: its durability or charge went down.
internal sealed record AuctionItemView(string Key, string Name, bool Used, int Count);
