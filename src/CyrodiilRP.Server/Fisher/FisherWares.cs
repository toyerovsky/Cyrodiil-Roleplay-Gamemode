using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Server.Fisher;

// What the fisher sells: every grade of rod, good, and the baits the mode makes, at their catalogue prices.
internal static class FisherWares
{
    private const string BasicRodIcon = "rod";
    private const string SturdyRodIcon = "rod-sturdy";
    private const string MasterRodIcon = "rod-master";

    public static readonly IReadOnlyList<FisherWare> All =
    [
        .. RodCatalog.All.Select(ToWare),
        .. BaitCatalog.All.Where(bait => bait.Price > 0).Select(ToWare),
    ];

    // Null for a key the fisher does not sell.
    public static FisherWare? Find(string key) => All.FirstOrDefault(ware => ware.Key == key);

    public static string FindIcon(RodTier tier) => tier switch
    {
        RodTier.Basic => BasicRodIcon,
        RodTier.Sturdy => SturdyRodIcon,
        RodTier.Master => MasterRodIcon,
    };

    private static FisherWare ToWare(RodKind rod) =>
        new(rod.Key, FisherWareKind.Rod, FindIcon(rod.Tier), rod.Name, rod.Price, rod.Weight, rod.ReachCm, rod.Line);

    private static FisherWare ToWare(BaitItem bait) => new(
        bait.CustomKey ?? throw new InvalidOperationException($"The fisher sells {bait.Name}, which is no custom item of the mode."),
        FindKind(bait.Kind), bait.Icon, bait.Name, bait.Price, bait.Weight, ReachCm: null, Line: null);

    private static FisherWareKind FindKind(BaitKind kind) => kind switch
    {
        BaitKind.Worms or BaitKind.Dough or BaitKind.CrabMeat => FisherWareKind.Bait,
        BaitKind.Lure => FisherWareKind.Lure,
        BaitKind.None => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No bait is no ware."),
    };
}
