using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Items;
using ReadyM.Relay.Common.Oblivion.Rpc;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Economy;

// The game's own gold, saved with the inventory. Each method returns false while the character is not in the world.
[Service]
internal sealed partial class Wallet(IEntities entities, Inventories inventories)
{
    public bool TryCount(PlayerId player, out int gold)
    {
        gold = 0;
        if (!entities.TryLookup(player, out MainCharacter main))
            return false;

        gold = inventories.CountCarried(GetInventory(main), CreateGoldStack(1));
        return true;
    }

    public bool IsInWorld(PlayerId player) => entities.TryLookup(player, out MainCharacter _);

    // The inventory adds counts as they come, so a stack past int.MaxValue would wrap to a debt.
    public bool TryGive(PlayerId player, int amount)
    {
        if (amount <= 0 || !entities.TryLookup(player, out MainCharacter main))
            return false;

        var inventory = GetInventory(main);
        if (inventories.CountCarried(inventory, CreateGoldStack(1)) > int.MaxValue - amount)
            return false;

        inventories.Add(inventory, new InventoryEntry(Items.Gold, amount, 0));
        return true;
    }

    public bool TryTake(PlayerId player, int amount)
        => amount > 0
           && entities.TryLookup(player, out MainCharacter main)
           && inventories.TryTake(GetInventory(main), CreateGoldStack(amount));

    private static Equipment GetInventory(in MainCharacter main) => EntityHandle.Of(main).As<Equipment>();

    private static ItemStackData CreateGoldStack(int count) => new()
    {
        BaseId = Items.Gold,
        Count = count,
        Durability = InventoryEntry.NoInstanceOverride,
        Charge = InventoryEntry.NoInstanceOverride,
    };
}
