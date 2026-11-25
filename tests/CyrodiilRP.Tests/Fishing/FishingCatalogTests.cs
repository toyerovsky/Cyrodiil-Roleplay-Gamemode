using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Tests.Fishing;

public sealed class FishingCatalogTests
{
    [Fact]
    public void Every_species_has_one_kind_and_every_item_key_is_used_once_across_fish_rods_and_baits()
    {
        Assert.Equal(Enum.GetValues<FishSpecies>().Order(), FishCatalog.All.Select(fish => fish.Species).Order());

        var keys = FishCatalog.Items.Select(item => item.Key)
            .Concat(RodCatalog.Items.Select(rod => rod.Key))
            .Concat(BaitCatalog.All.Select(bait => bait.CustomKey).OfType<string>())
            .ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.StartsWith("fishing.", key, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_item_is_made_of_an_item_of_the_games_the_mode_names()
    {
        Assert.NotNull(ItemCatalog.Shipped.FindByForm(FishingRules.FishBase.Value));
        Assert.NotNull(ItemCatalog.Shipped.FindByForm(FishingRules.FishIcon.Value));
        Assert.NotNull(ItemCatalog.Shipped.FindByForm(RodCatalog.Base.Value));
        Assert.All(BaitCatalog.All, bait => Assert.NotNull(ItemCatalog.Shipped.FindByForm(bait.Base.Value)));
    }

    [Fact]
    public void A_fish_of_middle_length_weighs_about_what_such_a_fish_does()
    {
        foreach (var fish in FishCatalog.All)
        {
            var middle = fish.GramsAt(fish.LengthAt(0.5));
            Assert.InRange(middle, 20, 8000);
        }
    }

    [Fact]
    public void A_trophy_is_its_fish_worth_three_times_more()
    {
        var pike = FishCatalog.Find(FishSpecies.Pike);
        var trophy = FishCatalog.FindItem(pike.TrophyKey)!;

        Assert.True(trophy.Trophy);
        Assert.Equal("Trophy Pike", trophy.Name);
        Assert.Equal(pike.Value * 3, trophy.Value);
        Assert.Equal("pike", pike.Icon);
        Assert.Null(FishCatalog.FindItem("fishing.rod"));
    }

    [Fact]
    public void A_rod_wears_good_worn_damaged_broken_and_no_further()
    {
        var rod = RodCatalog.Find(RodTier.Sturdy).In(RodCondition.Good);
        var steps = new List<RodItem> { rod };
        while (steps[^1].WornDown is { } next)
            steps.Add(next);

        Assert.Equal(Enum.GetValues<RodCondition>(), steps.Select(step => step.Condition));
        Assert.Equal(["fishing.rod.sturdy", "fishing.rod.sturdy.worn", "fishing.rod.sturdy.damaged", "fishing.rod.sturdy.broken"], steps.Select(step => step.Key));
        Assert.Equal("Sturdy Fishing Rod (Damaged)", steps[2].Name);
        Assert.False(steps[^1].Usable);
        Assert.All(steps.Zip(steps.Skip(1)), pair => Assert.True(pair.First.Line > pair.Second.Line && pair.First.Value > pair.Second.Value));
    }

    [Theory]
    [InlineData("Good", 0)]
    [InlineData("Worn", 4)]
    [InlineData("Damaged", 8)]
    [InlineData("Broken", 12)]
    public void Mending_the_plain_rod_costs_a_share_of_its_price_for_each_step_it_wore(string condition, int price)
    {
        Assert.Equal(price, RodCatalog.Find(RodTier.Basic).RepairPrice(Enum.Parse<RodCondition>(condition)));
    }

    [Fact]
    public void Every_rod_item_is_found_by_its_key_and_a_better_grade_reaches_farther_with_a_stronger_line()
    {
        Assert.All(RodCatalog.Items, rod => Assert.Equal(rod, RodCatalog.FindItem(rod.Key)));
        Assert.Equal(RodCatalog.All.OrderBy(rod => rod.Tier), RodCatalog.All.OrderBy(rod => rod.ReachCm));
        Assert.Equal(RodCatalog.All.OrderBy(rod => rod.Tier), RodCatalog.All.OrderBy(rod => rod.Line));
    }

    [Fact]
    public void Every_bait_but_none_has_one_item_and_only_the_games_crab_meat_is_not_sold()
    {
        Assert.Equal(Enum.GetValues<BaitKind>().Where(kind => kind != BaitKind.None).Order(), BaitCatalog.All.Select(bait => bait.Kind).Order());
        Assert.Throws<ArgumentOutOfRangeException>(() => BaitCatalog.Find(BaitKind.None));
        Assert.All(BaitCatalog.All, bait => Assert.Equal(bait.CustomKey is null, bait.Price == 0));
        Assert.Equal(BaitCatalog.Find(BaitKind.Lure), BaitCatalog.FindByKey("fishing.lure"));
    }
}
