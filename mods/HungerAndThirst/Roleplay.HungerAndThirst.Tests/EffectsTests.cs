using OblivionMp.Sdk.Common.Statuses;
using Roleplay.HungerAndThirst.Server;
using Xunit;

namespace Roleplay.HungerAndThirst.Tests;

public class EffectsTests
{
    private static readonly SustenanceConfig Config = new();
    private static readonly NaturalVitals Natural = new(Health: 200f, Magicka: 100f, Fatigue: 300f);

    private static Tier[] Hunger => SustenanceEffects.Resolve(Config.Hunger, problem => Assert.Fail(problem));
    private static Tier[] Thirst => SustenanceEffects.Resolve(Config.Thirst, problem => Assert.Fail(problem));

    [Theory]
    [InlineData(1000, -1)]
    [InlineData(500, -1)]  // below 50%, not at it
    [InlineData(499, 2)]   // Hungry: tiers are deepest first
    [InlineData(249, 1)]   // Starving
    [InlineData(1, 1)]
    [InlineData(0, 0)]     // Wasting Away, only once empty
    public void A_meter_is_in_the_deepest_tier_it_is_below(int shown, int tier)
    {
        Assert.Equal(tier, SustenanceEffects.TierAt(Hunger, shown));
    }

    [Fact]
    public void Percent_effects_scale_with_the_characters_own_maximum()
    {
        Tier hungry = Hunger[2];
        var wanted = SustenanceEffects.Wanted(hungry, null, Natural, health: 200f, healthFloor: 10, healthDamage: true);

        Assert.Contains(wanted, w => w.Effect == StatusEffect.Of(StatusKind.Drain, Stat.Fatigue, 60).WithName("Hungry"));
        Assert.Contains(wanted, w => w.Effect == StatusEffect.Of(StatusKind.Drain, Stat.Willpower, 5).WithName("Hungry"));
    }

    [Fact]
    public void Fatigue_drains_from_both_meters_and_their_attributes_stop_at_one_left()
    {
        var wanted = SustenanceEffects.Wanted(Hunger[0], Thirst[0], Natural, 200f, 10, true);

        int fatigue = wanted.Where(w => w.Vital == Stat.Fatigue).Sum(w => w.Effect.Magnitude);
        int attributes = wanted
            .Where(w => w.Effect.Stat is { } stat && SustenanceEffects.FeedsInto(stat) == Stat.Fatigue)
            .Sum(w => w.Effect.Magnitude);
        Assert.Equal(85, attributes);   // Strength 25, Endurance 25, Willpower 15, Agility 20
        Assert.Equal(299 - 85, fatigue);
    }

    [Fact]
    public void Drain_health_stops_at_the_floor_and_kills_without_one()
    {
        Tier wasting = Hunger[0];

        int Drained(float health, int floor) => SustenanceEffects.Wanted(wasting, null, Natural, health, floor, true)
            .Where(w => w.Vital == Stat.Health).Sum(w => w.Effect.Magnitude);

        Assert.Equal(50, Drained(200f, 10));   // 25% of 200
        Assert.Equal(40, Drained(50f, 10));    // only down to 10
        Assert.Equal(0, Drained(8f, 10));      // already below: no drain at all
        Assert.Equal(50, Drained(50f, 0));     // no floor: it may kill
    }

    [Fact]
    public void Damage_health_comes_off_at_the_floor()
    {
        Tier dehydrated = Thirst[0];

        Assert.Contains(SustenanceEffects.Wanted(null, dehydrated, Natural, 200f, 10, healthDamage: true),
            w => w.Effect == StatusEffect.Of(StatusKind.Damage, Stat.Health, 1).WithName("Dehydrated"));
        Assert.DoesNotContain(SustenanceEffects.Wanted(null, dehydrated, Natural, 200f, 10, healthDamage: false),
            w => w.Vital == Stat.Health);
    }

    [Theory]
    [InlineData("Drain Fatigue", true)]
    [InlineData("drain hand to hand", true)]
    [InlineData("Burden", true)]
    [InlineData("FTHR", true)]
    [InlineData("Damage Blade", false)]   // the game has no Damage for skills
    [InlineData("Sap Fatigue", false)]
    [InlineData("Burden please", false)]
    public void Effects_are_read_by_kind_and_stat_or_by_code(string effect, bool readable)
    {
        Assert.Equal(readable, SustenanceEffects.TryRead(new TierEffect { Effect = effect, Points = 5 }, out _, out _));
    }

    [Fact]
    public void A_percent_needs_a_vital()
    {
        Assert.False(SustenanceEffects.TryRead(new TierEffect { Effect = "Burden", Percent = 10 }, out _, out _));
        Assert.False(SustenanceEffects.TryRead(new TierEffect { Effect = "Drain Strength", Percent = 10 }, out _, out _));
    }
}
