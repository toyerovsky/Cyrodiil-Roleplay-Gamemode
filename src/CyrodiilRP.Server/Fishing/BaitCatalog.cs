using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Server.Fishing;

// The baits: the mode's own made as copies of the game's covered pot, bowl and spoon (so nobody eats them), and the
// game's crab meat as it is.
internal static class BaitCatalog
{
    public static readonly IReadOnlyList<BaitItem> All =
    [
        new(BaitKind.Worms, "Fishing Worms", "fishing.worms", new FormId(0x000105DF), BaitUse.PerCast, Price: 1, Weight: 0.1f, Value: 1, Icon: "worms"),
        new(BaitKind.Dough, "Dough Bait", "fishing.dough", new FormId(0x000105D5), BaitUse.PerCast, Price: 2, Weight: 0.1f, Value: 1, Icon: "dough"),
        new(BaitKind.CrabMeat, "Crab Meat", null, new FormId(0x0003366C), BaitUse.PerCast, Price: 0, Weight: 0.1f, Value: 1, Icon: "crabmeat"),
        new(BaitKind.Lure, "Spoon Lure", "fishing.lure", new FormId(0x00019119), BaitUse.LostWithLine, Price: 15, Weight: 0.1f, Value: 7, Icon: "spinner"),
    ];

    // Every kind but None.
    public static BaitItem Find(BaitKind kind) => kind == BaitKind.None
        ? throw new ArgumentOutOfRangeException(nameof(kind), kind, "No bait is no item.")
        : All.Single(bait => bait.Kind == kind);

    // Null for a key that is no custom bait's.
    public static BaitItem? FindByKey(string key) => All.FirstOrDefault(bait => bait.CustomKey == key);
}
