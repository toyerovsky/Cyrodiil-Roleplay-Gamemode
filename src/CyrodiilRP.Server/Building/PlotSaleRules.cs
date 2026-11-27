namespace CyrodiilRP.Server.Building;

// Who may buy a plot at its sign. Nothing here reads the world or moves gold.
internal static class PlotSaleRules
{
    // How far from a plot's sign its window stays open, in centimetres.
    public const float WalkAway = 500f;

    public const int WalkAwayMetres = (int)(WalkAway / 100f);

    public static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(1);

    public static PlotStanding StandingOf(PlotRecord plot, long characterId) => plot.OwnerCharacterId switch
    {
        null => PlotStanding.ForSale,
        var owner when owner == characterId => PlotStanding.Yours,
        _ => PlotStanding.Taken,
    };

    // Owned: how many plots the buyer owns now. Most: how many a character may own.
    public static SaleVerdict Judge(PlotRecord plot, long characterId, int owned, int most, int gold) => StandingOf(plot, characterId) switch
    {
        PlotStanding.Taken => SaleVerdict.Taken,
        PlotStanding.Yours => SaleVerdict.Yours,
        PlotStanding.ForSale when owned >= most => SaleVerdict.TooMany,
        PlotStanding.ForSale when gold < plot.Price => SaleVerdict.TooPoor,
        PlotStanding.ForSale => SaleVerdict.Accepted,
    };
}
