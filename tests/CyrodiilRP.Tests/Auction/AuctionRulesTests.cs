using CyrodiilRP.Data.Auction;

namespace CyrodiilRP.Tests.Auction;

public sealed class AuctionRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Extension = TimeSpan.FromMinutes(5);

    [Theory]
    [InlineData(1000, 5, 50)]
    [InlineData(99, 5, 4)]
    [InlineData(19, 5, 0)]
    [InlineData(1000, 0, 0)]
    [InlineData(int.MaxValue, 50, int.MaxValue / 2)]
    public void The_house_cut_rounds_down(int price, int percent, int cut) => Assert.Equal(cut, AuctionRules.Cut(price, percent));

    [Theory]
    [InlineData(100, 2, 2)]
    [InlineData(101, 2, 3)]
    [InlineData(10, 2, 1)]
    [InlineData(1, 50, 1)]
    [InlineData(1000, 0, 0)]
    public void The_listing_fee_rounds_up_and_is_at_least_one_gold(int price, int percent, int fee) => Assert.Equal(fee, AuctionRules.Fee(price, percent));

    [Fact]
    public void The_first_bid_is_the_starting_price()
    {
        Assert.Equal(50, AuctionRules.FindMinimumBid(50, null, 5));
    }

    [Theory]
    [InlineData(100, 5, 105)]
    [InlineData(10, 5, 11)]
    [InlineData(10, 0, 11)]
    [InlineData(int.MaxValue, 5, int.MaxValue)]
    public void Each_next_bid_goes_a_step_over_the_top_and_at_least_one_gold(int top, int step, int minimum) =>
        Assert.Equal(minimum, AuctionRules.FindMinimumBid(1, top, step));

    [Fact]
    public void A_bid_near_the_end_moves_the_end_to_the_extension_after_it()
    {
        Assert.Equal(Now + Extension, AuctionRules.FindEnd(Now + TimeSpan.FromMinutes(2), Now, Extension));
    }

    [Fact]
    public void A_bid_long_before_the_end_leaves_it()
    {
        var end = Now + TimeSpan.FromMinutes(10);

        Assert.Equal(end, AuctionRules.FindEnd(end, Now, Extension));
        Assert.Equal(end, AuctionRules.FindEnd(end, end - TimeSpan.FromSeconds(1), TimeSpan.Zero));
    }

    [Fact]
    public void A_parcel_holds_gold_for_a_sale_and_a_bid_and_the_item_for_everything_else()
    {
        var gold = new[] { AuctionParcelReason.Sale, AuctionParcelReason.Outbid, AuctionParcelReason.Refund };

        Assert.All(Enum.GetValues<AuctionParcelReason>(), reason => Assert.Equal(gold.Contains(reason), AuctionRules.CarriesGold(reason)));
    }
}
