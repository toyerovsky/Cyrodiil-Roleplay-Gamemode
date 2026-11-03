namespace CyrodiilRP.Server.Traders;

// SeenGold: the gold the window last showed.
internal sealed class OpenTrader(TraderSpotRecord spot, TraderShop shop)
{
    public TraderSpotRecord Spot { get; } = spot;

    public TraderShop Shop { get; } = shop;

    public int SeenGold { get; set; } = -1;
}
