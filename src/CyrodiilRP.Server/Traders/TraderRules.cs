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

    // The shop a stablemaster's spot is stored under. No shop of stocks.json may take the name.
    public const string StableShop = "stable";

    // The shops a mentor's or the registrar's spot is stored under. No shop of stocks.json may take these names.
    public const string WarriorMentorShop = "mentor.warrior";
    public const string MageMentorShop = "mentor.mage";
    public const string SupportMentorShop = "mentor.support";
    public const string RegistrarShop = "registrar";

    private static readonly IReadOnlyList<FormId> ShopOutfit = [Items.Clothes.MiddleShirt01, Items.Clothes.MiddlePants01, Items.Clothes.MiddleShoes01];
    private static readonly IReadOnlyList<FormId> BankerOutfit = [Items.Clothes.UpperShirt01, Items.Clothes.UpperPants01, Items.Clothes.UpperShoes01];

    // What Petrine of Bruma's stables wears.
    private static readonly IReadOnlyList<FormId> StablemasterOutfit = [Items.Clothes.LowerShirt10, Items.Clothes.LowerPants08, Items.Clothes.LowerShoes01];

    private static readonly IReadOnlyList<FormId> WarriorMentorOutfit = [Items.Clothes.LowerShirt10, Items.Clothes.MiddlePants01, Items.Clothes.MiddleShoes01];
    private static readonly IReadOnlyList<FormId> MageMentorOutfit = [Items.Clothes.UpperRobe02, Items.Clothes.MiddleShoes01];
    private static readonly IReadOnlyList<FormId> SupportMentorOutfit = [Items.Clothes.LowerRobe03, Items.Clothes.LowerShoes01];
    private static readonly IReadOnlyList<FormId> RegistrarOutfit = [Items.Clothes.UpperShirt04, Items.Clothes.UpperPants04, Items.Clothes.UpperShoes02];

    public const float WanderRadius = 50f;

    // A trader of ours this near its spot is the one standing there, whatever ref id the spot remembers.
    public const float AdoptRadius = 400f;

    // A trader whose cell is this many cells from its spot's, or nearer, is at home: wandering takes it over an edge.
    public const int HomeCells = 1;

    public static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan LoadRetry = TimeSpan.FromSeconds(10);

    public static uint FindOrigin(TraderSpotKind kind) => kind switch
    {
        TraderSpotKind.Shop => TraderOrigin.SpawnerRefId,
        TraderSpotKind.Banker => TraderOrigin.BankerSpawnerRefId,
        TraderSpotKind.Stablemaster => TraderOrigin.StablemasterSpawnerRefId,
        TraderSpotKind.WarriorMentor => TraderOrigin.WarriorMentorSpawnerRefId,
        TraderSpotKind.MageMentor => TraderOrigin.MageMentorSpawnerRefId,
        TraderSpotKind.SupportMentor => TraderOrigin.SupportMentorSpawnerRefId,
        TraderSpotKind.Registrar => TraderOrigin.RegistrarSpawnerRefId,
    };

    public static IReadOnlyList<FormId> FindOutfit(TraderSpotKind kind) => kind switch
    {
        TraderSpotKind.Shop => ShopOutfit,
        TraderSpotKind.Banker => BankerOutfit,
        TraderSpotKind.Stablemaster => StablemasterOutfit,
        TraderSpotKind.WarriorMentor => WarriorMentorOutfit,
        TraderSpotKind.MageMentor => MageMentorOutfit,
        TraderSpotKind.SupportMentor => SupportMentorOutfit,
        TraderSpotKind.Registrar => RegistrarOutfit,
    };

    // A person the mode stood, of any kind.
    public static bool IsOurs(uint spawnerRefId) =>
        spawnerRefId is TraderOrigin.SpawnerRefId or TraderOrigin.BankerSpawnerRefId or TraderOrigin.StablemasterSpawnerRefId
            or TraderOrigin.WarriorMentorSpawnerRefId or TraderOrigin.MageMentorSpawnerRefId or TraderOrigin.SupportMentorSpawnerRefId
            or TraderOrigin.RegistrarSpawnerRefId;

    // The kind a spot stands by the shop it is stored under.
    public static TraderSpotKind FindKind(string shop) => shop switch
    {
        BankShop => TraderSpotKind.Banker,
        StableShop => TraderSpotKind.Stablemaster,
        WarriorMentorShop => TraderSpotKind.WarriorMentor,
        MageMentorShop => TraderSpotKind.MageMentor,
        SupportMentorShop => TraderSpotKind.SupportMentor,
        RegistrarShop => TraderSpotKind.Registrar,
        _ => TraderSpotKind.Shop,
    };

    // Rounded up, in whole gold.
    public static int ComputePrice(int value) => (value * MarkupPercent + 99) / 100;

    private const float CentimetresPerMetre = 100f;
}
