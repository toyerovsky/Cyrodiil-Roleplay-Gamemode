using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Items;
using ReadyM.Relay.Common.Oblivion.Rpc;

namespace CyrodiilRP.Server.Exchange;

// The game keeps the copies of an item apart by their durability and charge. Key: how a page names the stack.
internal readonly record struct ExchangeStack(uint Form, float Health, float Charge)
{
    public static ExchangeStack Gold { get; } = new(Items.Gold.Value, InventoryEntry.NoInstanceOverride, InventoryEntry.NoInstanceOverride);

    public string Key => $"{Form:X8}:{BitConverter.SingleToUInt32Bits(Health):X8}:{BitConverter.SingleToUInt32Bits(Charge):X8}";

    // No longer as the game made it, which the one taking it should see.
    public bool IsUsed => Health != InventoryEntry.NoInstanceOverride || Charge != InventoryEntry.NoInstanceOverride;

    public ItemStackData CreateData(int count) => new() { BaseId = new FormId(Form), Count = count, Durability = Health, Charge = Charge };
}
