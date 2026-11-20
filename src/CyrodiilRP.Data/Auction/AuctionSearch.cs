namespace CyrodiilRP.Data.Auction;

// Text: part of an item's name, null for any. Types: the item records wanted, empty for every one. Page: from 0.
public sealed record AuctionSearch(string? Text, IReadOnlyList<string> Types, int Page, DateTimeOffset Now);
