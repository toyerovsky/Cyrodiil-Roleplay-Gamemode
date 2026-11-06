using CyrodiilRP.Common.Traders;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.Items;

namespace CyrodiilRP.Server.Traders;

// Traders only sell: nothing a player brings makes money out of nothing.
internal static class TraderRules
{
    // On top of the game's own value of a ware, as a percentage.
    public const int MarkupPercent = 140;

    // No ware the game values higher: better gear comes from the players.
    public const int MostValue = 300;

    public const int MostPieces = 20;

    // A step more than the wheel reaches, before the window closes.
    public const int WalkAwayMetres = 5;
    public const float WalkAway = WalkAwayMetres * CentimetresPerMetre;

    // The game's "Miner" (FGC06DeadMiner1), an Imperial commoner: no services, so the game offers no trade or room of its
    // own and the window is the only way to buy, no faction, AI package, script or line of dialogue of his own, and the
    // quest only lays his placed copies dead. The game gives a person made by the server nothing to wear.
    public const uint BaseNpc = 0x000025FA;

    // The shop a banker's spot is stored under. No shop of stocks.json may take the name.
    public const string BankShop = "bank";

    private static readonly IReadOnlyList<FormId> ShopOutfit = [Items.Clothes.MiddleShirt01, Items.Clothes.MiddlePants01, Items.Clothes.MiddleShoes01];
    private static readonly IReadOnlyList<FormId> BankerOutfit = [Items.Clothes.UpperShirt01, Items.Clothes.UpperPants01, Items.Clothes.UpperShoes01];

    public const float WanderRadius = 50f;

    // A trader of ours this near its spot is the one standing there, whatever ref id the spot remembers.
    public const float AdoptRadius = 400f;

    // A trader whose cell is this many cells from its spot's, or nearer, is at home: wandering takes it over an edge.
    public const int HomeCells = 1;

    public static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(2);

    // Health back to full this often, so only a blow larger than the whole bar kills a trader.
    public static readonly TimeSpan RefillEvery = TimeSpan.FromMilliseconds(100);

    public static readonly TimeSpan LoadRetry = TimeSpan.FromSeconds(10);

    public static uint FindOrigin(TraderSpotKind kind) => kind switch
    {
        TraderSpotKind.Shop => TraderOrigin.SpawnerRefId,
        TraderSpotKind.Banker => TraderOrigin.BankerSpawnerRefId,
    };

    public static IReadOnlyList<FormId> FindOutfit(TraderSpotKind kind) => kind switch
    {
        TraderSpotKind.Shop => ShopOutfit,
        TraderSpotKind.Banker => BankerOutfit,
    };

    // A person the mode stood, of either kind.
    public static bool IsOurs(uint spawnerRefId) => spawnerRefId is TraderOrigin.SpawnerRefId or TraderOrigin.BankerSpawnerRefId;

    // Rounded up, in whole gold.
    public static int ComputePrice(int value) => (value * MarkupPercent + 99) / 100;

    private const float CentimetresPerMetre = 100f;
}
