using CyrodiilRP.Server.Exchange;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Auction;

// The items of a lot between the character and the house, with the durability and charge they had. Each method returns
// false while the character is not in the world.
[Service]
internal sealed partial class AuctionInventory(IEntities entities, Inventories inventories)
{
    // False too when the character carries fewer than count, unworn.
    public bool TryTake(PlayerId player, ExchangeStack stack, int count) =>
        entities.TryLookup(player, out MainCharacter main) && inventories.TryTake(GetInventory(main), stack.CreateData(count));

    public bool TryGive(PlayerId player, ExchangeStack stack, int count)
    {
        if (count <= 0 || !entities.TryLookup(player, out MainCharacter main))
            return false;

        var entry = new InventoryEntry(stack.Item, count, 0) { Health = stack.Health, Charge = stack.Charge };
        inventories.Add(GetInventory(main), entry);
        return true;
    }

    private static Equipment GetInventory(in MainCharacter main) => EntityHandle.Of(main).As<Equipment>();
}
