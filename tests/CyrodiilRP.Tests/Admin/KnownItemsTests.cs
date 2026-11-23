using CyrodiilRP.Server.Admin;
using CyrodiilRP.Tests.Fakes;
using OblivionMP.SDK.Common.Items;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Tests.Admin;

public sealed class KnownItemsTests
{
    private const int Most = 10;

    private static readonly ItemCatalog Catalog = ItemCatalog.Load(
        """
        [
          { "id": "WeapIronBattleAxe", "form": "00000D7F", "name": "Iron Battle Axe", "type": "WEAP" },
          { "id": "StoneBrick", "form": "00025946", "name": "Stone Brick", "type": "MISC" },
          { "id": "Firebrand", "form": "00003000", "name": "Firebrand", "type": "WEAP" }
        ]
        """);

    private readonly FakeCustomItems _custom = new();
    private readonly NetworkFormId _firewood;
    private readonly NetworkFormId _axe;

    public KnownItemsTests()
    {
        _firewood = _custom.Add("jobs.firewood", CustomItem.CopyOf(new FormId(0x00025946)).WithName("Firewood"));
        _axe = _custom.Add("jobs.lumber-axe", CustomItem.CopyOf(new FormId(0x00000D7F)).WithName("Lumber Axe"));
    }

    [Fact]
    public void A_custom_item_is_named_by_its_definition_and_typed_as_the_game_item_it_copies()
    {
        Assert.Equal(new KnownItem("custom:jobs.firewood", _firewood, "Firewood", "MISC"), KnownItems.Describe(_firewood, Catalog, _custom));
        Assert.Equal("WEAP", KnownItems.Describe(_axe, Catalog, _custom)?.Type);
    }

    [Fact]
    public void A_game_item_is_named_by_the_catalogue()
    {
        var axe = new FormId(0x00000D7F);

        Assert.Equal(new KnownItem("WeapIronBattleAxe", axe, "Iron Battle Axe", "WEAP"), KnownItems.Describe(axe, Catalog, _custom));
    }

    [Fact]
    public void Form_zero_with_no_definition_and_a_form_the_catalogue_lacks_name_nothing()
    {
        Assert.Null(KnownItems.Describe(default(NetworkFormId), Catalog, _custom));
        Assert.Null(KnownItems.Describe(new FormId(0x00099999), Catalog, _custom));
    }

    [Fact]
    public void The_game_items_alone_leave_every_custom_item_out()
    {
        Assert.Null(KnownItems.DescribeGameItem(_firewood, Catalog));
        Assert.Equal("StoneBrick", KnownItems.DescribeGameItem(new FormId(0x00025946), Catalog)?.Id);
    }

    [Theory]
    [InlineData("custom:jobs.firewood")]
    [InlineData("Firewood")]
    [InlineData("firewood")]
    public void A_custom_item_is_found_by_its_id_or_its_name(string text)
    {
        Assert.Equal(_firewood, KnownItems.Find(text, Catalog, _custom).One?.Item);
    }

    [Fact]
    public void A_search_lists_the_custom_items_first_and_keeps_to_the_type()
    {
        Assert.Equal(["Firewood", "Firebrand"], KnownItems.Search("fire", null, Most, Catalog, _custom).Select(item => item.Name));
        Assert.Equal(["Lumber Axe", "Firebrand", "Iron Battle Axe"], KnownItems.Search(null, "WEAP", Most, Catalog, _custom).Select(item => item.Name));
        Assert.Empty(KnownItems.Search(" ", null, Most, Catalog, _custom));
    }
}
