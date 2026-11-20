using CyrodiilRP.Server;

namespace CyrodiilRP.Tests.Auction;

public sealed class AuctionSettingsTests
{
    [Fact]
    public void The_defaults_hold() => new AuctionSettings().Check();

    [Theory]
    [InlineData(nameof(AuctionSettings.ListingFeePercent), AuctionSettings.MostPercent + 1)]
    [InlineData(nameof(AuctionSettings.ListingFeePercent), -1)]
    [InlineData(nameof(AuctionSettings.CutPercent), AuctionSettings.MostPercent + 1)]
    [InlineData(nameof(AuctionSettings.BidStepPercent), -1)]
    [InlineData(nameof(AuctionSettings.MaxLots), 0)]
    [InlineData(nameof(AuctionSettings.MaxLots), AuctionSettings.MostLots + 1)]
    [InlineData(nameof(AuctionSettings.ExtendMinutes), -1)]
    [InlineData(nameof(AuctionSettings.ExtendMinutes), AuctionSettings.MostExtendMinutes + 1)]
    public void A_number_out_of_its_range_stops_the_mod(string name, int value)
    {
        var settings = new AuctionSettings();
        typeof(AuctionSettings).GetProperty(name)!.SetValue(settings, value);

        Assert.Throws<InvalidOperationException>(settings.Check);
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { AuctionSettings.MostHours + 1 })]
    [InlineData(new[] { 24, 24 })]
    public void Lengths_that_are_none_out_of_range_or_twice_stop_the_mod(int[] hours)
    {
        Assert.Throws<InvalidOperationException>(new AuctionSettings { DurationHours = [.. hours] }.Check);
    }
}
