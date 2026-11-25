using System.Text.Json;
using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Fisher;
using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Tests.Fisher;

public sealed class FisherViewsTests
{
    private const int Gold = 120;
    private const int Worms = 12;

    private static readonly FishItem PikeTrophy = FishCatalog.FindItem(FishCatalog.Find(FishSpecies.Pike).TrophyKey)!;
    private static readonly RodKind BasicRod = RodCatalog.Find(RodTier.Basic);
    private static readonly RodKind SturdyRod = RodCatalog.Find(RodTier.Sturdy);

    [Fact]
    public void The_fisher_sells_every_grade_of_rod_good_and_the_modes_own_baits_but_not_the_games_crab_meat()
    {
        Assert.Equal(
            [.. RodCatalog.All.Select(rod => rod.Key), "fishing.worms", "fishing.dough", "fishing.lure"],
            FisherWares.All.Select(ware => ware.Key));
        Assert.All(FisherWares.All, ware => Assert.True(ware.Price > 0, ware.Key));
        Assert.Null(FisherWares.Find(BasicRod.In(RodCondition.Worn).Key));
        Assert.Null(FisherWares.Find(PikeTrophy.Key));
    }

    [Fact]
    public void Every_grade_of_rod_has_a_painted_icon_of_its_own()
    {
        var icons = Enum.GetValues<RodTier>().Select(FisherWares.FindIcon).ToList();

        Assert.Equal(["rod", "rod-sturdy", "rod-master"], icons);
    }

    [Fact]
    public void The_window_names_its_fields_as_the_page_reads_them()
    {
        var root = Serialize(new FisherGoods(Gold,
            [new CarriedFish(PikeTrophy, 1)],
            [new CarriedRod(BasicRod.In(RodCondition.Worn), 1)],
            [new CarriedBait(BaitCatalog.Find(BaitKind.Worms), Worms)]));

        Assert.Equal(Gold, root.GetProperty("gold").GetInt32());
        Assert.Equal(FisherRules.MostPieces, root.GetProperty("most").GetInt32());

        var caught = Assert.Single(root.GetProperty("catch").EnumerateArray().ToList());
        Assert.Equal("fishing.pike.trophy", caught.GetProperty("item").GetString());
        Assert.Equal("Pike", caught.GetProperty("species").GetString());
        Assert.Equal("Trophy Pike", caught.GetProperty("name").GetString());
        Assert.True(caught.GetProperty("trophy").GetBoolean());
        Assert.Equal(1, caught.GetProperty("count").GetInt32());
        Assert.Equal(PikeTrophy.Value, caught.GetProperty("price").GetInt32());

        var sturdy = FindWare(root, SturdyRod.Key);
        Assert.Equal("Rod", sturdy.GetProperty("kind").GetString());
        Assert.Equal("rod-sturdy", sturdy.GetProperty("icon").GetString());
        Assert.Equal("Sturdy Fishing Rod", sturdy.GetProperty("name").GetString());
        Assert.Equal(SturdyRod.Price, sturdy.GetProperty("price").GetInt32());
        Assert.Equal(SturdyRod.Weight, sturdy.GetProperty("weight").GetSingle());
        Assert.Equal(SturdyRod.ReachCm, sturdy.GetProperty("reachCm").GetSingle());
        Assert.Equal(SturdyRod.Line, sturdy.GetProperty("line").GetDouble());
        Assert.Equal(JsonValueKind.Null, sturdy.GetProperty("count").ValueKind);

        var worms = FindWare(root, "fishing.worms");
        Assert.Equal("Bait", worms.GetProperty("kind").GetString());
        Assert.Equal("worms", worms.GetProperty("icon").GetString());
        Assert.Equal(Worms, worms.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Null, worms.GetProperty("reachCm").ValueKind);
        Assert.Equal(JsonValueKind.Null, worms.GetProperty("line").ValueKind);

        var lure = FindWare(root, "fishing.lure");
        Assert.Equal("Lure", lure.GetProperty("kind").GetString());
        Assert.Equal("spinner", lure.GetProperty("icon").GetString());
        Assert.Equal(0, lure.GetProperty("count").GetInt32());

        var repair = Assert.Single(root.GetProperty("repairs").EnumerateArray().ToList());
        Assert.Equal("fishing.rod.worn", repair.GetProperty("item").GetString());
        Assert.Equal("rod", repair.GetProperty("icon").GetString());
        Assert.Equal("Fishing Rod (Worn)", repair.GetProperty("name").GetString());
        Assert.Equal("Worn", repair.GetProperty("condition").GetString());
        Assert.Equal(FisherRules.FindRepairPrice(BasicRod.In(RodCondition.Worn)), repair.GetProperty("price").GetInt32());
    }

    [Fact]
    public void Only_worn_damaged_and_broken_rods_are_offered_for_mending()
    {
        var rods = Enum.GetValues<RodCondition>().Select(condition => new CarriedRod(SturdyRod.In(condition), 1)).ToList();

        var view = FisherViews.Build(new FisherGoods(Gold, [], rods, []));

        Assert.Equal([RodCondition.Worn, RodCondition.Damaged, RodCondition.Broken], view.Repairs.Select(repair => repair.Condition));
        Assert.All(view.Repairs, repair => Assert.Equal("rod-sturdy", repair.Icon));
    }

    [Fact]
    public void An_empty_pack_still_shows_the_whole_shop()
    {
        var view = FisherViews.Build(new FisherGoods(0, [], [], []));

        Assert.Empty(view.Catch);
        Assert.Empty(view.Repairs);
        Assert.Equal(FisherWares.All.Count, view.Shop.Count);
    }

    private static JsonElement Serialize(FisherGoods goods) =>
        JsonSerializer.SerializeToElement(FisherViews.Build(goods), FisherJson.Default.FisherView);

    private static JsonElement FindWare(JsonElement root, string key) =>
        root.GetProperty("shop").EnumerateArray().Single(ware => ware.GetProperty("ware").GetString() == key);
}
