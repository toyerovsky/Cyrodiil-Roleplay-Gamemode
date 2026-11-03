using CyrodiilRP.Server.Characters;
using CyrodiilRP.Tests.Fakes;
using OblivionMp.Sdk.Serverside.Items;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Items;

namespace CyrodiilRP.Tests.Characters;

public sealed class StartingKitTests
{
    private const byte Worn = 1;
    private const byte Carried = 0;
    private const int StartingGold = 50;

    // What the tutorial left in a save: a staff and a sword in hand, a bow, arrows, gold, a jail shirt worn.
    private static readonly FormId Staff = new(0x000243E4);
    private static readonly FormId Sword = new(0x00025230);
    private static readonly FormId Bow = new(0x00025233);
    private static readonly FormId Arrows = new(0x00017829);
    private static readonly FormId JailShirt = new(0x00000017);

    private readonly List<InventoryEntry> _items = [];

    [Fact]
    public void A_save_s_items_give_way_to_the_kit_worn_ones_too()
    {
        _items.AddRange([
            new InventoryEntry(Staff, 1, Worn), new InventoryEntry(Sword, 1, Carried), new InventoryEntry(Bow, 1, Carried),
            new InventoryEntry(Arrows, 66, Worn), new InventoryEntry(Items.Gold, 57, Carried), new InventoryEntry(JailShirt, 1, Worn),
        ]);

        StartingKit.Dress(new FakeInventory(_items));

        Assert.Equal(StartingKit.Clothes, _items.Select(item => item.FormId.FormId));
        Assert.All(_items, item =>
        {
            Assert.Equal(1, item.Count);
            Assert.Equal(Worn, item.Worn);
            Assert.Equal(InventoryEntry.NoInstanceOverride, item.Health);
            Assert.Equal(InventoryEntry.NoInstanceOverride, item.Charge);
        });
    }

    [Fact]
    public void An_empty_inventory_gets_the_kit()
    {
        StartingKit.Dress(new FakeInventory(_items));

        Assert.Equal(StartingKit.Clothes, _items.Select(item => item.FormId.FormId));
    }

    // A creation that failed is dressed again when the creator closes again.
    [Fact]
    public void Dressing_twice_leaves_one_kit()
    {
        StartingKit.Dress(new FakeInventory(_items));
        StartingKit.Dress(new FakeInventory(_items));

        Assert.Equal(StartingKit.Clothes, _items.Select(item => item.FormId.FormId));
    }

    [Fact]
    public void The_starting_gold_given_after_the_kit_stays()
    {
        _items.Add(new InventoryEntry(Items.Gold, 57, Carried));
        var inventory = new FakeInventory(_items);

        StartingKit.Dress(inventory);
        new Inventories().Add(inventory, new InventoryEntry(Items.Gold, StartingGold, Carried));

        Assert.Equal(StartingGold, _items.Single(item => item.FormId.FormId == Items.Gold).Count);
        Assert.Equal(StartingKit.Clothes.Count + 1, _items.Count);
    }

    // The commoners' shirt, trousers and shoes of Oblivion.esm, one piece for each of the three places.
    [Fact]
    public void The_kit_is_a_shirt_trousers_and_shoes()
    {
        Assert.Equal([new FormId(0x0001C82E), new FormId(0x0001C82C), new FormId(0x0001C82F)], StartingKit.Clothes);
    }
}
