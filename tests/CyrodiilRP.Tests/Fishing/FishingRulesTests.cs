using System.Text.Json;
using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Server.Traders;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Fishing;

public sealed class FishingRulesTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void The_shares_of_the_finds_make_a_whole_and_every_find_is_named_once()
    {
        Assert.Equal(1.0, FishingRules.Finds.Sum(find => find.Share), Tolerance);
        Assert.Equal(Enum.GetValues<FindKindName>().Order(), FishingRules.Finds.Select(find => find.Kind).Order());
    }

    [Fact]
    public void Every_find_is_an_item_of_the_games_the_mode_names()
    {
        foreach (var find in FishingRules.Finds)
            Assert.NotNull(ItemCatalog.Shipped.FindByForm(find.Item.Value));
    }

    [Fact]
    public void The_general_trader_sells_the_plain_rod_the_fishing_makes()
    {
        var plain = RodCatalog.Find(RodTier.Basic).In(RodCondition.Good);
        var rod = WareCatalog.Shipped.FindShop("general")!.Wares.Single(ware => ware.CustomKey == plain.Key);
        var names = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(Repository.Root, "locales", "en", "ui.wares.json")))!;

        Assert.Equal(plain.Rod.Weight, rod.Weight);
        Assert.Equal(plain.Name, names[$"ui.wares.{rod.Id}"]);
        Assert.NotNull(ItemCatalog.Shipped.FindByForm(RodCatalog.Base.Value));
    }

    [Fact]
    public void A_roll_under_the_find_share_brings_a_find_and_one_at_it_a_fish()
    {
        Assert.Equal(FishingRules.Finds[0], FishingRules.FindOf(0.0));
        Assert.Equal(FishingRules.Finds[^1], FishingRules.FindOf(FishingRules.FindShare - Tolerance));
        Assert.Null(FishingRules.FindOf(FishingRules.FindShare));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.0)]
    [InlineData(double.NaN)]
    public void A_roll_outside_zero_to_one_is_refused(double roll)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FishingRules.FindOf(roll));
    }

    [Fact]
    public void A_fishs_length_spans_its_kind_and_its_weight_follows_from_it()
    {
        var perch = FishCatalog.Find(FishSpecies.Perch);

        Assert.Equal(perch.ShortestCm, perch.LengthAt(0));
        Assert.Equal(perch.LongestCm, perch.LengthAt(1));
        // A perch of 25 cm weighs about 200 g.
        Assert.InRange(perch.GramsAt(25), 190, 210);
    }

    [Fact]
    public void The_waits_run_from_their_soonest_to_their_latest()
    {
        Assert.Equal(FishingRules.BiteSoonest, FishingRules.BiteWait(0));
        Assert.Equal(FishingRules.BiteLatest, FishingRules.BiteWait(1));
        Assert.Equal(FishingRules.ThrashSoonest, FishingRules.ThrashWait(0));
        Assert.Equal(FishingRules.ThrashLatest, FishingRules.ThrashWait(1));
    }
}
