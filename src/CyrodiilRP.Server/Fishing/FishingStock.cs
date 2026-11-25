using CyrodiilRP.Server.Players;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Rpc;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Fishing;

// The fishing's things in a character's pack: rods, baits and the catch, by the custom items' keys. Every method finds
// nothing, or does nothing and says false, while the character is not in the world or the item is not made yet.
[Service]
internal sealed partial class FishingStock(ICustomItems customItems, IEntities entities, Inventories inventories)
{
    public bool TryFind(string key, out NetworkFormId item) => customItems.TryGet(key, out item);

    public bool TryFind(BaitItem bait, out NetworkFormId item)
    {
        if (bait.CustomKey is { } key)
            return customItems.TryGet(key, out item);

        item = new FormId(bait.Base.Value);
        return true;
    }

    public int Count(OnlinePlayer player, NetworkFormId item) =>
        TryFindPack(player, out var pack) ? inventories.CountCarried(pack, Stack(item, 1)) : 0;

    public int Count(OnlinePlayer player, string key) => TryFind(key, out var item) ? Count(player, item) : 0;

    public bool TryTake(OnlinePlayer player, NetworkFormId item, int count) =>
        count > 0 && TryFindPack(player, out var pack) && inventories.TryTake(pack, Stack(item, count));

    public bool TryGive(OnlinePlayer player, NetworkFormId item, int count)
    {
        if (count <= 0 || !TryFindPack(player, out var pack))
            return false;

        inventories.Add(pack, new InventoryEntry(item, count, 0));
        return true;
    }

    // One piece of the first key for one of the second, as a rod wears or is mended.
    public bool TryExchange(OnlinePlayer player, string fromKey, string toKey) =>
        TryFind(fromKey, out var from) && TryFind(toKey, out var to) && TryTake(player, from, 1) && TryGive(player, to, 1);

    public IReadOnlyList<CarriedRod> ListRods(OnlinePlayer player) =>
        RodCatalog.Items
            .Select(rod => new CarriedRod(rod, Count(player, rod.Key)))
            .Where(carried => carried.Count > 0)
            .ToList();

    // The best rod that fishes: the highest grade, then the best condition.
    public RodItem? FindBestRod(OnlinePlayer player) =>
        ListRods(player)
            .Select(carried => carried.Rod)
            .Where(rod => rod.Usable)
            .OrderByDescending(rod => rod.Rod.Tier)
            .ThenBy(rod => rod.Condition)
            .FirstOrDefault();

    public IReadOnlyList<CarriedBait> ListBaits(OnlinePlayer player) =>
        BaitCatalog.All
            .Select(bait => new CarriedBait(bait, TryFind(bait, out var item) ? Count(player, item) : 0))
            .Where(carried => carried.Count > 0)
            .ToList();

    public IReadOnlyList<CarriedFish> ListCatch(OnlinePlayer player) =>
        FishCatalog.Items
            .Select(fish => new CarriedFish(fish, Count(player, fish.Key)))
            .Where(carried => carried.Count > 0)
            .ToList();

    private bool TryFindPack(OnlinePlayer player, out Equipment pack)
    {
        pack = default;
        if (!entities.TryLookup(player.Id, out MainCharacter main))
            return false;

        pack = EntityHandle.Of(main).As<Equipment>();
        return true;
    }

    private static ItemStackData Stack(NetworkFormId item, int count) => new()
    {
        BaseId = item,
        Count = count,
        Durability = InventoryEntry.NoInstanceOverride,
        Charge = InventoryEntry.NoInstanceOverride,
    };
}
