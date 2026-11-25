using CyrodiilRP.Server.Players;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Items;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Fishing;

// The fish as items every game knows: defined as the server starts, kept in the world's save with the items carried.
[Service]
internal sealed partial class FishItems(ICustomItems customItems, IEntities entities, Inventories inventories)
{
    private void Start()
    {
        foreach (var fish in FishingRules.Fish)
            customItems.Define(fish.ItemKey, CustomItem.CopyOf(FishingRules.FishBase)
                .WithName(fish.ItemName)
                .WithValue(fish.Value)
                .WithWeight(fish.Weight)
                .WithMesh(FishingRules.FishMesh)
                .WithIconOf(FishingRules.FishIcon));
    }

    // False when the character is not in the world, or the fish is not made yet in this run.
    public bool TryGive(OnlinePlayer player, FishKind fish) =>
        customItems.TryGet(fish.ItemKey, out var item) && TryAdd(player, new InventoryEntry(item, 1, 0));

    public bool TryGive(OnlinePlayer player, FindKind find) => TryAdd(player, new InventoryEntry(find.Item, 1, 0));

    private bool TryAdd(OnlinePlayer player, InventoryEntry entry)
    {
        if (!entities.TryLookup(player.Id, out MainCharacter main))
            return false;

        inventories.Add(EntityHandle.Of(main).As<Equipment>(), entry);
        return true;
    }
}
