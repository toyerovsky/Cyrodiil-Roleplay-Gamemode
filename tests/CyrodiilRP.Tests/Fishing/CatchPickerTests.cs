using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Tests.Fishing;

public sealed class CatchPickerTests
{
    private const int Lowest = FishingSkillRules.LowestLevel;
    private const int Highest = FishingSkillRules.HighestLevel;
    private const float Near = FarCastRules.NearAt;
    private const float Far = FarCastRules.FarAt;

    // A cast to where the far cast's size skew is 1: the skill's own skew sizes the fish.
    private static readonly float Middling = Near + (Far - Near) * (float)((FarCastRules.SkewNear - 1) / (FarCastRules.SkewNear - FarCastRules.SkewFar));

    private static readonly FishingConditions DayInRumare = new(WaterKind.LakeRumare, DayPart.Day, WeatherKind.Clear);

    [Fact]
    public void Every_species_has_one_habitat_its_factors_above_nothing_and_its_shares_not_below()
    {
        Assert.Equal(Enum.GetValues<FishSpecies>().Order(), FishHabitatCatalog.All.Select(habitat => habitat.Species).Order());
        foreach (var habitat in FishHabitatCatalog.All)
        {
            Assert.All(Enum.GetValues<WaterKind>(), water => Assert.True(habitat.Waters.Of(water) >= 0));
            Assert.All(Enum.GetValues<DayPart>(), hour => Assert.True(habitat.Hours.Of(hour) > 0));
            Assert.All(Enum.GetValues<WeatherKind>(), weather => Assert.True(habitat.Weathers.Of(weather) > 0));
            Assert.All(Enum.GetValues<BaitKind>(), bait => Assert.True(habitat.Baits.Of(bait) > 0));
            Assert.Contains(Enum.GetValues<WaterKind>(), water => habitat.Waters.Of(water) > 0);
        }
    }

    [Fact]
    public void Something_bites_in_every_water_at_every_hour_in_every_weather_on_every_bait_from_the_first_level()
    {
        foreach (var water in Enum.GetValues<WaterKind>())
        foreach (var hour in Enum.GetValues<DayPart>())
        foreach (var weather in Enum.GetValues<WeatherKind>())
        foreach (var bait in Enum.GetValues<BaitKind>())
            Assert.NotEmpty(CatchPicker.Weigh(new FishingConditions(water, hour, weather), bait, Lowest, Middling));
    }

    [Theory]
    [InlineData("LakeRumare", new[] { "Carp", "GoldenCarp", "Perch", "Pike" }, new[] { "Longfin", "Seabass" })]
    [InlineData("NibenBay", new[] { "Histcarp", "Eel", "Seabass" }, new[] { "GoldenCarp" })]
    [InlineData("Sea", new[] { "Longfin", "Seabass", "Spadetail", "Slaughterfish" }, new[] { "Perch", "Carp", "Trout", "Pike", "GoldenCarp", "Catfish" })]
    [InlineData("River", new[] { "Trout", "Betty", "Pike", "Histcarp" }, new[] { "Longfin", "Seabass", "GoldenCarp" })]
    [InlineData("Lake", new[] { "Trout", "Perch", "Pike" }, new[] { "Longfin", "Seabass", "GoldenCarp" })]
    [InlineData("Stream", new[] { "Betty", "Trout" }, new[] { "Slaughterfish", "Catfish", "GoldenCarp", "Longfin" })]
    [InlineData("Underground", new[] { "Eel", "Catfish", "Slaughterfish" }, new[] { "Pike", "Longfin", "Seabass", "GoldenCarp" })]
    public void Each_water_yields_its_own_kinds_and_never_the_others(string water, string[] yields, string[] never)
    {
        var species = CatchPicker.Weigh(new FishingConditions(Enum.Parse<WaterKind>(water), DayPart.Night, WeatherKind.Rain), BaitKind.Worms, Highest, Middling)
            .Select(weight => weight.Fish.Species.ToString())
            .ToList();

        Assert.All(yields, kind => Assert.Contains(kind, species));
        Assert.All(never, kind => Assert.DoesNotContain(kind, species));
    }

    [Fact]
    public void The_golden_carp_bites_only_in_lake_rumare_and_only_from_its_level()
    {
        var golden = FishCatalog.Find(FishSpecies.GoldenCarp);

        Assert.DoesNotContain(CatchPicker.Weigh(DayInRumare, BaitKind.Dough, golden.MinLevel - 1, Middling), weight => weight.Fish == golden);
        Assert.Contains(CatchPicker.Weigh(DayInRumare, BaitKind.Dough, golden.MinLevel, Middling), weight => weight.Fish == golden);
        foreach (var water in Enum.GetValues<WaterKind>().Where(water => water != WaterKind.LakeRumare))
            Assert.DoesNotContain(CatchPicker.Weigh(DayInRumare with { Water = water }, BaitKind.Dough, Highest, Middling), weight => weight.Fish == golden);
    }

    [Fact]
    public void No_species_bites_below_its_level()
    {
        foreach (var fish in FishCatalog.All.Where(fish => fish.MinLevel > Lowest))
        foreach (var water in Enum.GetValues<WaterKind>())
            Assert.DoesNotContain(CatchPicker.Weigh(DayInRumare with { Water = water }, BaitKind.Worms, fish.MinLevel - 1, Middling), weight => weight.Fish == fish);
    }

    [Fact]
    public void Eels_and_catfish_bite_at_night_rather_than_by_day_and_the_histcarp_in_the_rain()
    {
        var underground = new FishingConditions(WaterKind.Underground, DayPart.Day, WeatherKind.Clear);
        var bay = new FishingConditions(WaterKind.NibenBay, DayPart.Day, WeatherKind.Clear);

        Assert.True(Share(underground with { Hour = DayPart.Night }, FishSpecies.Eel) > 2 * Share(underground, FishSpecies.Eel));
        Assert.True(Share(underground with { Hour = DayPart.Night }, FishSpecies.Catfish) > 2 * Share(underground, FishSpecies.Catfish));
        Assert.True(Share(bay with { Weather = WeatherKind.Rain }, FishSpecies.Histcarp) > 1.5 * Share(bay, FishSpecies.Histcarp));
        Assert.True(Share(new FishingConditions(WaterKind.River, DayPart.Dawn, WeatherKind.Clear), FishSpecies.Trout)
                    > Share(new FishingConditions(WaterKind.River, DayPart.Day, WeatherKind.Clear), FishSpecies.Trout));
    }

    [Fact]
    public void A_roll_under_the_find_share_brings_a_find_and_the_rest_a_sized_species()
    {
        var find = CatchPicker.Pick(DayInRumare, BaitKind.Worms, Lowest, Middling, Rolls(0.0));
        var fish = CatchPicker.Pick(DayInRumare, BaitKind.Worms, Lowest, Middling, Rolls(FishingRules.FindShare, 0.0, 0.5));

        Assert.Equal(new FindQuarry(FishingRules.Finds[0]), find);
        var caught = Assert.IsType<FishQuarry>(fish);
        Assert.Equal(CatchPicker.Weigh(DayInRumare, BaitKind.Worms, Lowest, Middling)[0].Fish, caught.Fish);
        Assert.Equal(Math.Pow(0.5, FishingSkillRules.SizeSkew(Lowest)), caught.Share, 6);
    }

    [Fact]
    public void A_far_cast_lands_bigger_fish_from_the_same_roll_than_a_near_one()
    {
        var near = (FishQuarry)CatchPicker.Pick(DayInRumare, BaitKind.Worms, Lowest, Near, Rolls(0.5, 0.0, 0.8));
        var far = (FishQuarry)CatchPicker.Pick(DayInRumare, BaitKind.Worms, Lowest, Far, Rolls(0.5, 0.0, 0.8));

        Assert.Equal(near.Fish, far.Fish);
        Assert.Equal(Math.Pow(0.8, FishingSkillRules.SizeSkew(Lowest) * FarCastRules.SkewNear), near.Share, 6);
        Assert.Equal(Math.Pow(0.8, FishingSkillRules.SizeSkew(Lowest) * FarCastRules.SkewFar), far.Share, 6);
        Assert.True(far.LengthCm > near.LengthCm);
    }

    [Fact]
    public void A_rare_fish_bites_likelier_the_farther_the_cast_and_the_others_keep_their_weights()
    {
        var night = new FishingConditions(WaterKind.Underground, DayPart.Night, WeatherKind.Clear);
        var near = CatchPicker.Weigh(night, BaitKind.Worms, Highest, Near);
        var far = CatchPicker.Weigh(night, BaitKind.Worms, Highest, Far);

        Assert.Contains(near, weight => weight.Fish.Rare);
        Assert.Contains(near, weight => !weight.Fish.Rare);
        foreach (var close in near)
        {
            var farther = far.Single(weight => weight.Fish == close.Fish);
            var grows = close.Fish.Rare ? FarCastRules.RareFar / FarCastRules.RareNear : 1;
            Assert.Equal(close.Weight * grows, farther.Weight, 9);
        }
    }

    [Theory]
    [InlineData(0f, 0.0)]
    [InlineData(Near, 0.0)]
    [InlineData((Near + Far) / 2, 0.5)]
    [InlineData(Far, 1.0)]
    [InlineData(Far + 500f, 1.0)]
    public void A_cast_is_as_far_as_it_lies_between_the_near_and_the_far_mark(float distanceCm, double share)
    {
        Assert.Equal(share, FarCastRules.FarShare(distanceCm), 6);
        Assert.Equal(FarCastRules.SkewNear + (FarCastRules.SkewFar - FarCastRules.SkewNear) * share, FarCastRules.SizeSkew(distanceCm), 6);
        Assert.Equal(FarCastRules.RareNear + (FarCastRules.RareFar - FarCastRules.RareNear) * share, FarCastRules.RareWeight(distanceCm), 6);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void A_cast_that_lands_nowhere_is_refused(float distanceCm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FarCastRules.FarShare(distanceCm));
    }

    [Fact]
    public void Every_rod_reaches_past_the_near_mark_and_only_the_best_to_the_far_one()
    {
        Assert.All(RodCatalog.All, rod => Assert.True(rod.ReachCm > Near));
        Assert.True(RodCatalog.Find(RodTier.Basic).ReachCm < Far);
        Assert.True(RodCatalog.Find(RodTier.Sturdy).ReachCm < Far);
        Assert.True(RodCatalog.Find(RodTier.Master).ReachCm >= Far);
    }

    [Fact]
    public void A_skilled_angler_lands_bigger_fish_from_the_same_roll()
    {
        var novice = (FishQuarry)CatchPicker.Pick(DayInRumare, BaitKind.Worms, Lowest, Middling, Rolls(0.5, 0.0, 0.8));
        var master = (FishQuarry)CatchPicker.Pick(DayInRumare, BaitKind.Worms, Highest, Middling, Rolls(0.5, 0.0, 0.8));

        Assert.True(master.LengthCm > novice.LengthCm);
        Assert.Equal(master.Share >= FishingRules.TrophyFrom, master.Trophy);
    }

    [Fact]
    public void A_bare_hook_waits_longer_and_a_better_rod_and_more_skill_shorter()
    {
        var plain = RodCatalog.Find(RodTier.Basic);
        var master = RodCatalog.Find(RodTier.Master);

        Assert.Equal(1.0, CatchPicker.WaitFactor(Lowest, plain, BaitKind.Worms));
        Assert.Equal(FishingRules.BareHookWaits, CatchPicker.WaitFactor(Lowest, plain, BaitKind.None));
        Assert.Equal(1.0 / master.BiteSpeed, CatchPicker.WaitFactor(Lowest, master, BaitKind.Lure), 6);
        Assert.Equal(FishingSkillRules.WaitAtHighest, CatchPicker.WaitFactor(Highest, plain, BaitKind.Dough), 6);
    }

    private static double Share(FishingConditions conditions, FishSpecies species)
    {
        var weights = CatchPicker.Weigh(conditions, BaitKind.Worms, Highest, Middling);
        return weights.Single(weight => weight.Fish.Species == species).Weight / weights.Sum(weight => weight.Weight);
    }

    private static Func<double> Rolls(params double[] rolls)
    {
        var queue = new Queue<double>(rolls);
        return () => queue.Count > 0 ? queue.Dequeue() : throw new InvalidOperationException("The picker rolled more than the test gave it.");
    }
}
