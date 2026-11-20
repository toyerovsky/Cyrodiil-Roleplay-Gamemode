using System.Text.Json;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Crafting;

namespace CyrodiilRP.Tests.Crafting;

public sealed class CraftCatalogTests
{
    private const uint IronDagger = 0x00019171;
    private const uint SteelDagger = 0x000229B9;
    private const uint Lockpick = 0x0000000A;

    private const string Items =
        """
        [
          { "id": "WeapIronDagger", "form": "00019171", "name": "Iron Dagger", "type": "WEAP" },
          { "id": "WeapSteelDagger", "form": "000229B9", "name": "Steel Dagger", "type": "WEAP" },
          { "id": "Lockpick", "form": "0000000A", "name": "Lockpick", "type": "MISC" }
        ]
        """;

    private const string NoTables = "{}";
    private const string MetalTable = """{ "metal": { "Iron": 7, "Steel": 30 } }""";
    private const string Rest = """{ "type": "WEAP", "class": "Material" }, { "type": "MISC", "class": "Exempt" }""";

    private static readonly TradeRank[] RanksBelowMaster = [TradeRank.Novice, TradeRank.Apprentice, TradeRank.Journeyman, TradeRank.Expert];

    [Fact]
    public void Every_item_of_the_game_has_a_class()
    {
        Assert.All(ItemCatalog.Shipped.All, item => Assert.NotNull(CraftCatalog.Shipped.Find(item.Form)));
    }

    [Fact]
    public void Only_a_product_has_a_trade_and_a_level()
    {
        Assert.All(ItemCatalog.Shipped.All, item =>
        {
            var classification = CraftCatalog.Shipped.Find(item.Form)!;

            Assert.Equal(classification.Class == ItemClass.Product, classification.Product is not null);
            if (classification.Product is { } product)
                Assert.InRange(product.Level, 0, TradeRules.MaxLevel);
        });
    }

    [Theory]
    [InlineData("WeapIronDagger", nameof(Trade.Blacksmith), 0)]
    [InlineData("WeapSteelLongswordFine", nameof(Trade.Blacksmith), 35)]
    [InlineData("WeapDaedricClaymore", nameof(Trade.Blacksmith), TradeRules.MaxLevel)]
    [InlineData("Lockpick", nameof(Trade.Blacksmith), 15)]
    [InlineData("WeapGlassBow", nameof(Trade.Woodworker), 75)]
    [InlineData("Arrow1Iron", nameof(Trade.Woodworker), 0)]
    [InlineData("DaedricCuirass", nameof(Trade.Armourer), TradeRules.MaxLevel)]
    [InlineData("ChainmailShield", nameof(Trade.Armourer), 30)]
    [InlineData("LeatherCuirass", nameof(Trade.Tailor), 15)]
    [InlineData("MiddleShirt03", nameof(Trade.Tailor), 30)]
    [InlineData("JewelryRing5Gold", nameof(Trade.Jeweller), 75)]
    [InlineData("SoulGemEmpty5Grand", nameof(Trade.Jeweller), 90)]
    [InlineData("AlembicJourneyMan", nameof(Trade.Jeweller), TradeRules.JourneymanLevel)]
    [InlineData("PotionRestoreHealthW", nameof(Trade.Alchemist), 0)]
    [InlineData("PotionCureParlaysis", nameof(Trade.Alchemist), 30)]
    [InlineData("PotionDamageHealthS", nameof(Trade.Alchemist), 90)]
    [InlineData("Breadloaf", nameof(Trade.Cook), 0)]
    [InlineData("DrinkWine6TamikaBest", nameof(Trade.Cook), 65)]
    [InlineData("EnchEbonyLongswordBlizzard", nameof(Trade.Enchanter), 88)]
    [InlineData("EncArrow1IronFireDamage", nameof(Trade.Enchanter), 0)]
    [InlineData("StaffofFireDamageGreater", nameof(Trade.Enchanter), 80)]
    [InlineData("ScrollStandardShield5Master", nameof(Trade.Scribe), TradeRules.MaxLevel)]
    [InlineData("Book1CheapGuideAnvil", nameof(Trade.Scribe), 0)]
    [InlineData("SE30MadnessArrow00", nameof(Trade.Woodworker), 5)]
    [InlineData("SE30MadnessArrow0", nameof(Trade.Woodworker), 15)]
    [InlineData("SEGrummiteShield", nameof(Trade.Armourer), 20)]
    [InlineData("SEDarkSeducerBowFrail", nameof(Trade.Woodworker), 62)]
    [InlineData("SEStaffGrummiteFireDamage", nameof(Trade.Enchanter), 30)]
    public void A_product_belongs_to_a_trade_from_a_level(string id, string trade, int level)
    {
        var product = Find(id).Product;

        Assert.NotNull(product);
        Assert.Equal(Enum.Parse<Trade>(trade), product.Trade);
        Assert.Equal(level, product.Level);
    }

    [Theory]
    [InlineData("Gem3RubyFlawed", nameof(ItemClass.Material))]
    [InlineData("Gem0SilverNugget", nameof(ItemClass.Material))]
    [InlineData("WeapIronDaggerRusty", nameof(ItemClass.Material))]
    [InlineData("Nightshade", nameof(ItemClass.Material))]
    [InlineData("SE04Felldew", nameof(ItemClass.Material))]
    [InlineData("SE07Greenmote", nameof(ItemClass.Material))]
    [InlineData("SE07RendilsUnrefinedGreenmote", nameof(ItemClass.Relic))]
    [InlineData("MS18Chillrend01", nameof(ItemClass.Relic))]
    [InlineData("AmuletofKings", nameof(ItemClass.Relic))]
    [InlineData("ArenaIronBow", nameof(ItemClass.Relic))]
    [InlineData("Gold001", nameof(ItemClass.Exempt))]
    [InlineData("BoundSword", nameof(ItemClass.Exempt))]
    [InlineData("TestQASword", nameof(ItemClass.Exempt))]
    public void What_no_trade_makes_is_a_material_a_relic_or_exempt(string id, string itemClass)
    {
        Assert.Equal(new ItemClassification(Enum.Parse<ItemClass>(itemClass), null), Find(id));
    }

    // The Isles number their armour 1 to 6 from Impure to Perfect, and their weapons A to C and 1 to 5.
    [Theory]
    [InlineData("SEAmberBoots1", "SEAmberSwordA")]
    [InlineData("SEAmberCuirass2", "SEAmberSwordC")]
    [InlineData("SEMadnessHelmet3", "SEMadnessSword1")]
    [InlineData("SEMadnessShield4", "SEMadnessSword2")]
    [InlineData("SEAmberMagicBoots5", "SEAmberSword4")]
    [InlineData("SEMadnessGreaves6", "SEMadnessSword5")]
    public void An_isles_armour_piece_takes_the_level_of_the_weapon_of_its_grade(string armour, string weapon)
    {
        Assert.Equal(Find(weapon).Product!.Level, Find(armour).Product!.Level);
    }

    [Fact]
    public void Every_key_is_exempt()
    {
        var keys = ItemCatalog.Shipped.All.Where(item => item.Type == "KEYM").ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.Equal(ItemClass.Exempt, CraftCatalog.Shipped.Find(key.Form)!.Class));
    }

    [Theory]
    [InlineData("BladesCuirass", true)]
    [InlineData("SEAmberSword3", true)]
    [InlineData("SKLxBlade2", true)]
    [InlineData("WeapIronDagger", false)]
    [InlineData("EnchEbonyLongswordBlizzard", false)]
    public void The_gear_of_a_faction_or_of_the_isles_takes_a_pattern_besides_the_level(string id, bool needsPattern)
    {
        Assert.Equal(needsPattern, Find(id).Product!.NeedsPattern);
    }

    // So a character of any trade has something to make, and to learn on, from the first day to the last rank.
    [Fact]
    public void Every_trade_has_work_without_a_pattern_in_every_rank_below_master()
    {
        var ordinary = ItemCatalog.Shipped.All
            .Select(item => CraftCatalog.Shipped.Find(item.Form)!.Product)
            .OfType<CraftProduct>()
            .Where(product => !product.NeedsPattern)
            .ToList();

        Assert.All(Enum.GetValues<Trade>(), trade =>
        {
            var ranks = ordinary.Where(product => product.Trade == trade).Select(product => TradeRules.FindRank(product.Level)).ToHashSet();

            Assert.Contains(ordinary, product => product.Trade == trade && product.Level == 0);
            Assert.All(RanksBelowMaster, rank => Assert.Contains(rank, ranks));
        });
    }

    [Fact]
    public void The_first_rule_that_fits_an_item_classes_it()
    {
        var catalog = Load(NoTables, $$"""{ "id": "Iron", "class": "Relic" }, {{Rest}}""");

        Assert.Equal(ItemClass.Relic, catalog.Find(IronDagger)!.Class);
        Assert.Equal(ItemClass.Material, catalog.Find(SteelDagger)!.Class);
        Assert.Equal(ItemClass.Exempt, catalog.Find(Lockpick)!.Class);
    }

    [Fact]
    public void A_table_gives_the_level_by_the_key_in_the_id()
    {
        var catalog = Load(MetalTable, """{ "type": "WEAP", "id": "^Weap(?<key>Iron|Steel)Dagger$", "class": "Product", "trade": "Blacksmith", "table": "metal", "pattern": true }, { "type": "MISC", "class": "Exempt" }""");

        Assert.Equal(new ItemClassification(ItemClass.Product, new CraftProduct(Trade.Blacksmith, 7, true)), catalog.Find(IronDagger));
        Assert.Equal(new ItemClassification(ItemClass.Product, new CraftProduct(Trade.Blacksmith, 30, true)), catalog.Find(SteelDagger));
    }

    [Fact]
    public void A_form_the_game_lacks_has_no_class()
    {
        const uint CustomItem = 0xFF000001;

        Assert.Null(CraftCatalog.Shipped.Find(CustomItem));
    }

    [Fact]
    public void An_item_no_rule_classes_is_a_mistake_in_the_file()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => Load(NoTables, """{ "type": "MISC", "class": "Exempt" }"""));

        Assert.Contains("WeapIronDagger", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("WeapSteelDagger", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rule_that_classes_nothing_is_a_mistake_in_the_file()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => Load(NoTables, $$"""{{Rest}}, { "type": "BOOK", "class": "Relic" }"""));

        Assert.Contains("BOOK", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_table_no_rule_names_is_a_mistake_in_the_file()
    {
        Assert.Throws<InvalidOperationException>(() => Load(MetalTable, Rest));
    }

    [Fact]
    public void A_key_the_table_lacks_is_a_mistake_in_the_file()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => Load(
            """{ "metal": { "Iron": 7 } }""",
            """{ "type": "WEAP", "id": "^Weap(?<key>Iron|Steel)Dagger$", "class": "Product", "trade": "Blacksmith", "table": "metal" }, { "type": "MISC", "class": "Exempt" }"""));

        Assert.Contains("WeapSteelDagger", thrown.Message, StringComparison.Ordinal);
    }

    // Each with only the tables it names, so the file's other checks pass and the throw is the rule's own.
    [Theory]
    [InlineData(NoTables, """{ "class": "Relic" }""")]
    [InlineData(NoTables, """{ "type": "WEAP", "class": "Product", "level": 5 }""")]
    [InlineData(NoTables, """{ "type": "WEAP", "class": "Product", "trade": "Blacksmith" }""")]
    [InlineData(MetalTable, """{ "type": "WEAP", "id": "^Weap(?<key>Iron|Steel)", "class": "Product", "trade": "Blacksmith", "level": 5, "table": "metal" }""")]
    [InlineData(MetalTable, """{ "type": "WEAP", "id": "^Weap(Iron|Steel)", "class": "Product", "trade": "Blacksmith", "table": "metal" }""")]
    [InlineData(MetalTable, """{ "type": "WEAP", "class": "Product", "trade": "Blacksmith", "table": "metal" }""")]
    [InlineData(NoTables, """{ "type": "WEAP", "id": "^Weap(?<key>Iron|Steel)", "class": "Product", "trade": "Blacksmith", "table": "wood" }""")]
    [InlineData(NoTables, """{ "type": "WEAP", "class": "Product", "trade": "Blacksmith", "level": -1 }""")]
    [InlineData(NoTables, """{ "type": "WEAP", "class": "Product", "trade": "Blacksmith", "level": 101 }""")]
    [InlineData(NoTables, """{ "type": "WEAP", "class": "Material", "trade": "Blacksmith" }""")]
    [InlineData(NoTables, """{ "type": "WEAP", "class": "Relic", "level": 5 }""")]
    [InlineData(NoTables, """{ "type": "WEAP", "class": "Exempt", "pattern": true }""")]
    public void A_rule_that_does_not_hold_together_is_a_mistake_in_the_file(string tables, string rule)
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => Load(tables, $$"""{{rule}}, { "type": "MISC", "class": "Exempt" }"""));

        Assert.StartsWith("rule 1 ", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_in_a_table_outside_the_ranks_is_a_mistake_in_the_file()
    {
        Assert.Throws<InvalidOperationException>(() => Load(
            """{ "metal": { "Iron": 7, "Steel": 101 } }""",
            """{ "type": "WEAP", "id": "^Weap(?<key>Iron|Steel)Dagger$", "class": "Product", "trade": "Blacksmith", "table": "metal" }, { "type": "MISC", "class": "Exempt" }"""));
    }

    [Theory]
    [InlineData("""{ "type": "WEAP" }""")]
    [InlineData("""{ "type": "WEAP", "class": "Material", "patern": true }""")]
    [InlineData("""{ "type": "WEAP", "class": "Scrap" }""")]
    [InlineData("""{ "type": "WEAP", "class": "Product", "trade": "Smith", "level": 5 }""")]
    public void A_rule_with_a_word_the_file_does_not_know_or_without_a_class_is_not_read(string rule)
    {
        Assert.Throws<JsonException>(() => Load(NoTables, $$"""{{rule}}, { "type": "MISC", "class": "Exempt" }"""));
    }

    private static ItemClassification Find(string id) =>
        CraftCatalog.Shipped.Find(ItemCatalog.Shipped.All.Single(item => item.Id == id).Form)!;

    private static CraftCatalog Load(string tables, string rules) =>
        CraftCatalog.Load($$"""{ "tables": {{tables}}, "rules": [{{rules}}] }""", ItemCatalog.Load(Items));
}
