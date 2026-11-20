namespace CyrodiilRP.Data.Auction;

// The arithmetic of a lot in whole gold, shared by the book and the window, so what the window says is what is written.
public static class AuctionRules
{
    private const int Percent = 100;

    // The house's share of a price, rounded down.
    public static int Cut(int price, int percent) => (int)((long)price * percent / Percent);

    // A share of the highest price asked, rounded up, and at least 1 gold when there is a fee at all.
    public static int Fee(int highestPrice, int percent) =>
        percent == 0 ? 0 : (int)Math.Max(1, ((long)highestPrice * percent + Percent - 1) / Percent);

    // The starting price for the first bid, then the top bid and a step of at least 1 gold.
    public static int FindMinimumBid(int startPrice, int? topBid, int stepPercent) =>
        topBid is { } top ? (int)Math.Min(int.MaxValue, top + Math.Max(1, (long)top * stepPercent / Percent)) : startPrice;

    // A bid this near the end moves the end to that long after it, so nobody wins by bidding in the last second.
    public static DateTimeOffset FindEnd(DateTimeOffset endsAt, DateTimeOffset now, TimeSpan extension) =>
        endsAt - now < extension ? now + extension : endsAt;

    public static bool CarriesGold(AuctionParcelReason reason) => reason switch
    {
        AuctionParcelReason.Won or AuctionParcelReason.Bought or AuctionParcelReason.Unsold
            or AuctionParcelReason.Withdrawn or AuctionParcelReason.Removed => false,
        AuctionParcelReason.Sale or AuctionParcelReason.Outbid or AuctionParcelReason.Refund => true,
    };
}
