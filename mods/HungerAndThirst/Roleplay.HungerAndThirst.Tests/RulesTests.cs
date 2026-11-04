using Roleplay.HungerAndThirst.Common;
using Roleplay.HungerAndThirst.Server;
using Xunit;

namespace Roleplay.HungerAndThirst.Tests;

public class RulesTests
{
    private static readonly SustenanceConfig Config = new();

    [Fact]
    public void A_full_meter_empties_in_the_configured_time()
    {
        float perMinute = SustenanceRules.PerMinute(180f, 1f);
        double value = SustenanceScale.Max;
        for (int minute = 0; minute < 180; minute++)
            value = SustenanceRules.Decay(value, perMinute, 60);
        Assert.InRange(value, 0, 0.001); // the rate is a float: a hair is left, gone a moment later
    }

    [Fact]
    public void Zero_minutes_to_empty_means_no_decay()
    {
        Assert.Equal(0f, SustenanceRules.PerMinute(0f, 2f));
    }

    [Fact]
    public void The_largest_activity_multiplier_wins()
    {
        Assert.Equal(1f, SustenanceRules.ActivityMultiplier(false, false, false, Config));
        Assert.Equal(Config.RunningMultiplier, SustenanceRules.ActivityMultiplier(true, false, false, Config));
        Assert.Equal(Config.CombatMultiplier, SustenanceRules.ActivityMultiplier(true, true, true, Config));
    }

    [Fact]
    public void Decay_and_gain_stay_on_the_scale()
    {
        Assert.Equal(0, SustenanceRules.Decay(3, 100f, 60));
        Assert.Equal(SustenanceScale.Max, SustenanceRules.Gain(950, 200));
    }

    [Theory]
    [InlineData(1000.0, 1000)]
    [InlineData(999.2, 1000)]
    [InlineData(500.0, 500)]
    [InlineData(0.0001, 1)]
    [InlineData(0.0, 0)]
    public void The_shown_value_rounds_up_so_zero_means_empty(double value, int shown)
    {
        Assert.Equal(shown, SustenanceRules.Shown(value));
    }
}
