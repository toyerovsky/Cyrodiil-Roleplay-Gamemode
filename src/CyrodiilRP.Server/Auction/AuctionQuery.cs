namespace CyrodiilRP.Server.Auction;

// What a window searches the lots for. Text: part of an item's name, empty for any. Page: from 0.
internal sealed record AuctionQuery(string Text, AuctionCategory Category, int Page)
{
    public static AuctionQuery Everything { get; } = new(string.Empty, AuctionCategory.All, 0);
}
