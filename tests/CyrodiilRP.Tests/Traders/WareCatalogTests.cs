using CyrodiilRP.Server.Traders;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Traders;

public sealed class WareCatalogTests
{
    private const string Wares = """[{ "id": "Breadloaf", "form": "00033680", "value": 1, "weight": 0.5 }, { "id": "Lockpick", "form": "0000000A", "value": 3, "weight": 0 }]""";
    private const string Torch = """[{ "id": "Torch", "form": "0002CF9F", "value": 0, "weight": 0 }]""";

    // The price stocks.json gives the game's torch.
    private const int TorchPrice = 3;

    [Fact]
    public void The_shipped_shops_load_with_every_ware_priced_above_the_game_value()
    {
        var general = WareCatalog.Shipped.FindShop("general");

        Assert.NotNull(general);
        Assert.NotEmpty(general.Wares);
        Assert.All(general.Wares, ware => Assert.InRange(ware.Price, 1, TraderRules.ComputePrice(TraderRules.MostValue)));
        Assert.Equal(general.Wares.Count, general.Wares.Select(ware => ware.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(5, 7)]
    [InlineData(10, 14)]
    [InlineData(25, 35)]
    [InlineData(3, 5)]
    public void A_price_is_the_game_value_with_the_markup_rounded_up(int value, int price)
    {
        Assert.Equal(price, TraderRules.ComputePrice(value));
    }

    [Fact]
    public void A_shop_reads_the_form_id_and_the_category_it_puts_a_ware_in()
    {
        var shop = WareCatalog.Load(Wares, BuildStocks("""{ "camp": { "tools": ["Lockpick"], "food": ["Breadloaf"] } }""")).FindShop("camp")!;

        Assert.Equal(new Ware("Lockpick", 0x0000000A, "tools", 5, 0f), shop.Find("Lockpick"));
        Assert.Equal(0x00033680u, shop.Find("Breadloaf")!.FormId);
        Assert.Null(shop.Find("Skooma"));
    }

    [Fact]
    public void A_ware_the_game_lacks_or_values_at_nothing_is_a_mistake_in_the_files()
    {
        Assert.Throws<InvalidOperationException>(() => WareCatalog.Load(Wares, BuildStocks("""{ "camp": { "tools": ["Skooma"] } }""")));
        Assert.Throws<InvalidOperationException>(() => WareCatalog.Load(Torch, BuildStocks("""{ "camp": { "tools": ["Torch"] } }""")));
    }

    [Fact]
    public void A_shop_under_the_bankers_name_is_a_mistake_in_the_files() =>
        Assert.Throws<InvalidOperationException>(() => WareCatalog.Load(Wares, BuildStocks("""{ "bank": { "tools": ["Lockpick"] } }""")));

    [Fact]
    public void A_price_of_its_own_sells_a_ware_the_game_values_at_nothing_without_the_markup()
    {
        var shop = WareCatalog.Load(Torch, BuildStocks("""{ "camp": { "tools": ["Torch"] } }""", """{ "Torch": 3 }""")).FindShop("camp")!;

        Assert.Equal(3, shop.Find("Torch")!.Price);
    }

    [Fact]
    public void A_price_of_nothing_or_for_a_ware_no_shop_sells_is_a_mistake_in_the_files()
    {
        Assert.Throws<InvalidOperationException>(() => WareCatalog.Load(Torch, BuildStocks("""{ "camp": { "tools": ["Torch"] } }""", """{ "Torch": 0 }""")));
        Assert.Throws<InvalidOperationException>(() => WareCatalog.Load(Wares, BuildStocks("""{ "camp": { "tools": ["Lockpick"] } }""", """{ "Torch": 3 }""")));
    }

    [Fact]
    public void The_torch_is_sold_at_its_own_price()
    {
        Assert.Equal(TorchPrice, WareCatalog.Shipped.FindShop("general")!.Find("Torch02")!.Price);
    }

    // The page names every ware and shows its icon: the names and the atlas come from the same tool as wares.json.
    [Fact]
    public void Every_ware_sold_has_a_name_for_the_page()
    {
        var names = File.ReadAllText(Path.Combine(Repository.Root, "locales", "en", "ui.wares.json"));

        Assert.All(WareCatalog.Shipped.Shops.SelectMany(shop => shop.Wares), ware => Assert.Contains($"\"ui.wares.{ware.Id}\"", names, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_kind_of_ware_has_a_name_on_the_page()
    {
        var names = File.ReadAllText(Path.Combine(Repository.Root, "locales", "en", "ui.trader.json"));

        Assert.All(WareCatalog.Shipped.Shops.SelectMany(shop => shop.Wares).Select(ware => ware.Category).Distinct(),
            category => Assert.Contains($"\"ui.trader.category.{category}\"", names, StringComparison.Ordinal));
    }

    private static string BuildStocks(string shops, string prices = "{}") => $$"""{ "prices": {{prices}}, "shops": {{shops}} }""";
}
