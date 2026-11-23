using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Items;
using ReadyM.Relay.Common.Oblivion.Rpc;

namespace CyrodiilRP.Server.Exchange;

// The game keeps the copies of an item apart by their durability and charge. Item: whole, as a custom item's form id
// alone is 0. Key: how a page names the stack.
internal readonly record struct ExchangeStack(NetworkFormId Item, float Health, float Charge)
{
    public static ExchangeStack Gold { get; } = new(Items.Gold, InventoryEntry.NoInstanceOverride, InventoryEntry.NoInstanceOverride);

    public string Key => $"{ItemKey}:{BitConverter.SingleToUInt32Bits(Health):X8}:{BitConverter.SingleToUInt32Bits(Charge):X8}";

    // A game item by its form id, a custom item by its form entity, which NetworkFormId writes out.
    private string ItemKey => Item.FormId.Value != 0 ? $"{Item.FormId.Value:X8}" : Item.ToString();

    // No longer as the game made it, which the one taking it should see.
    public bool IsUsed => Health != InventoryEntry.NoInstanceOverride || Charge != InventoryEntry.NoInstanceOverride;

    public ItemStackData CreateData(int count) => new() { BaseId = Item, Count = count, Durability = Health, Charge = Charge };
}
