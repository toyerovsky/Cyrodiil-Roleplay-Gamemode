namespace CyrodiilRP.Server;

// The auction house. A price asks at most economy.maxPayment.
internal sealed class AuctionSettings
{
    public const int MostPercent = 50;
    public const int MostHours = 7 * 24;
    public const int MostLots = 100;
    public const int MostExtendMinutes = 60;

    // Of the highest price a lot asks, paid as it goes up and kept whatever becomes of it.
    public int ListingFeePercent { get; set; } = 2;

    // Of the price a lot sells for.
    public int CutPercent { get; set; } = 5;

    // The lengths a seller picks from.
    public List<int> DurationHours { get; set; } = [12, 24, 48];

    // Open lots a character may have at once.
    public int MaxLots { get; set; } = 10;

    // How far over the top bid the next one goes at least, and never less than 1 gold.
    public int BidStepPercent { get; set; } = 5;

    // A bid this near the end moves the end to this long after the bid. 0 lets a lot end when it was set to.
    public int ExtendMinutes { get; set; } = 5;

    public TimeSpan Extension => TimeSpan.FromMinutes(ExtendMinutes);

    // A wrong value stops the mod at start.
    public void Check()
    {
        if (ListingFeePercent is < 0 or > MostPercent || CutPercent is < 0 or > MostPercent || BidStepPercent is < 0 or > MostPercent)
            throw new InvalidOperationException($"auction in config.json: listingFeePercent, cutPercent and bidStepPercent go from 0 to {MostPercent}.");

        if (DurationHours.Count == 0 || DurationHours.Any(hours => hours is < 1 or > MostHours) || DurationHours.Distinct().Count() != DurationHours.Count)
            throw new InvalidOperationException($"auction in config.json: durationHours lists different lengths from 1 to {MostHours} hours.");

        if (MaxLots is < 1 or > MostLots || ExtendMinutes is < 0 or > MostExtendMinutes)
            throw new InvalidOperationException($"auction in config.json: maxLots goes from 1 to {MostLots}, extendMinutes from 0 to {MostExtendMinutes}.");
    }
}
