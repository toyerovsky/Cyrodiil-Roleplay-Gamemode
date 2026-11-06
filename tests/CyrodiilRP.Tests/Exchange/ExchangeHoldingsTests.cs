using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Tests.Fakes;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Tests.Exchange;

public sealed class ExchangeHoldingsTests
{
    private const uint Sword = 0x1000;
    private const uint Bread = 0x2000;
    private const uint NoCatalogue = 0x3000;
    private const uint PlayerMade = 0;
    private const int Gold = 75;
    private const byte Worn = 1;
    private const float Damaged = 120f;
    private const float Drained = 40f;
    private const float AsMade = InventoryEntry.NoInstanceOverride;

    private static readonly ItemCatalog Catalog = ItemCatalog.Load(
        """
        [
          { "id": "WeapIronSword", "form": "00001000", "name": "Iron Sword", "type": "WEAP" },
          { "id": "Breadloaf", "form": "00002000", "name": "Bread Loaf", "type": "INGR" },
          { "id": "Gold001", "form": "0000000F", "name": "Gold", "type": "MISC" }
        ]
        """);

    private readonly List<InventoryEntry> _items = [];

    [Fact]
    public void A_worn_copy_is_left_out_and_the_carried_ones_of_the_item_stay()
    {
        _items.Add(CreateEntry(Sword, 1, worn: Worn));
        _items.Add(CreateEntry(Sword, 2));

        var held = Read();

        Assert.Equal(2, Assert.Single(held.Items).Count);
    }

    [Fact]
    public void Gold_is_the_offer_of_its_own_and_no_item()
    {
        _items.Add(CreateEntry(ExchangeStack.Gold.Form, Gold));

        var held = Read();

        Assert.Empty(held.Items);
        Assert.Equal(Gold, held.Gold);
    }

    [Theory]
    [InlineData(NoCatalogue)]
    [InlineData(PlayerMade)]
    public void An_item_the_catalogue_does_not_name_is_left_out(uint form)
    {
        _items.Add(CreateEntry(form, 1));

        Assert.Empty(Read().Items);
    }

    [Fact]
    public void Copies_in_one_condition_make_one_stack_and_a_damaged_one_stays_apart()
    {
        _items.Add(CreateEntry(Sword, 1));
        _items.Add(CreateEntry(Sword, 2));
        _items.Add(CreateEntry(Sword, 1, Damaged));

        var held = Read();

        Assert.Equal(2, held.Items.Count);
        Assert.Equal(3, held.Items.Single(item => !item.Stack.IsUsed).Count);
        Assert.Equal(1, held.Items.Single(item => item.Stack.IsUsed).Count);
    }

    [Theory]
    [InlineData(Damaged, AsMade, true)]
    [InlineData(AsMade, Drained, true)]
    [InlineData(AsMade, AsMade, false)]
    public void A_stack_counts_as_used_once_its_durability_or_charge_went_down(float health, float charge, bool used)
    {
        Assert.Equal(used, new ExchangeStack(Sword, health, charge).IsUsed);
    }

    [Fact]
    public void Items_come_by_name()
    {
        _items.Add(CreateEntry(Sword, 1));
        _items.Add(CreateEntry(Bread, 1));

        Assert.Equal(["Bread Loaf", "Iron Sword"], Read().Items.Select(item => item.Item.Name));
    }

    [Fact]
    public void A_stack_is_found_by_the_key_the_page_got_and_nothing_by_any_other()
    {
        _items.Add(CreateEntry(Sword, 1));
        _items.Add(CreateEntry(Sword, 1, Damaged));
        var held = Read();
        var damaged = held.Items.Single(item => item.Stack.IsUsed);

        Assert.Same(damaged, held.Find(damaged.Stack.Key));
        Assert.Null(held.Find("not a key"));
    }

    [Fact]
    public void Holdings_match_until_a_count_or_the_gold_changes()
    {
        _items.Add(CreateEntry(Sword, 1));
        var seen = Read();

        Assert.True(Read().Matches(seen));

        _items.Add(CreateEntry(Sword, 1));
        Assert.False(Read().Matches(seen));
        Assert.False(ExchangeHoldings.Read(new FakeInventory(_items), Gold + 1, Catalog).Matches(Read()));
    }

    [Fact]
    public void A_damaged_stack_reaches_the_other_side_in_the_condition_it_left()
    {
        _items.Add(CreateEntry(Sword, 3, Damaged, Drained));
        var stack = Read().Items.Single().Stack;
        var taker = new List<InventoryEntry>();

        var moved = new Inventories().TryMove(new FakeInventory(_items), new FakeInventory(taker), stack.CreateData(2));

        Assert.True(moved);
        Assert.Equal(1, _items.Single().Count);
        Assert.Equal(stack, Assert.Single(ExchangeHoldings.Read(new FakeInventory(taker), 0, Catalog).Items).Stack);
    }

    private static InventoryEntry CreateEntry(uint form, int count, float health = AsMade, float charge = AsMade, byte worn = 0) =>
        new(new FormId(form), count, worn) { Health = health, Charge = charge };

    private ExchangeHoldings Read() => ExchangeHoldings.Read(new FakeInventory(_items), Gold, Catalog);
}
