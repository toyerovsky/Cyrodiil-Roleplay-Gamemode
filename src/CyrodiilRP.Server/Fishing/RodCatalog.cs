using CyrodiilRP.Common.Fishing;
using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Server.Fishing;

// The fishing rods: custom items made as copies of the game's oar, whose icon they keep, shown on the ground as the staff
// of their grade. The general trader sells the plain one (stocks.json), the fisher all three.
internal static class RodCatalog
{
    public static readonly FormId Base = new(0x00025102);

    public static readonly IReadOnlyList<RodKind> All =
    [
        new(RodTier.Basic, "fishing.rod", "Fishing Rod", ReachCm: 700f, Line: 1.0, BiteSpeed: 1.0, WearOneIn: 12, Price: 25, Weight: 3f),
        new(RodTier.Sturdy, "fishing.rod.sturdy", "Sturdy Fishing Rod", ReachCm: 1000f, Line: 1.25, BiteSpeed: 1.1, WearOneIn: 18, Price: 120, Weight: 3.5f),
        new(RodTier.Master, "fishing.rod.master", "Master Fishing Rod", ReachCm: 1300f, Line: 1.5, BiteSpeed: 1.2, WearOneIn: 26, Price: 450, Weight: 4f),
    ];

    private static readonly Dictionary<string, RodItem> ByKey = All
        .SelectMany(rod => Enum.GetValues<RodCondition>().Select(rod.In))
        .ToDictionary(item => item.Key, StringComparer.Ordinal);

    // Every rod in every condition.
    public static IReadOnlyCollection<RodItem> Items => ByKey.Values;

    public static RodKind Find(RodTier tier) => All.Single(rod => rod.Tier == tier);

    // Null for a key that is no rod's.
    public static RodItem? FindItem(string key) => ByKey.GetValueOrDefault(key);
}
