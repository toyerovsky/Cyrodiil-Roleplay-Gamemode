using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class DurabilityTests
{
    private readonly WoodcuttingConfig _woodcutting = new();
    private readonly MiningConfig _mining = new();

    [Theory]
    [InlineData(-1f, 168, 1f)]   // as made: the game keeps no health
    [InlineData(168f, 168, 1f)]
    [InlineData(84f, 168, 0.5f)]
    [InlineData(0f, 168, 0f)]    // broken
    [InlineData(200f, 168, 1f)]  // never over full
    [InlineData(10f, 0, 1f)]     // a durability the config does not know: counts as made
    public void A_tools_condition_is_its_share_of_full_durability(float health, int durability, float condition)
    {
        Assert.Equal(condition, HitDamage.ConditionOf(health, durability), 3);
    }

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(0.5f, 0f)]       // half: no glancing yet
    [InlineData(0.25f, 0.25f)]   // a quarter: one hit in four
    [InlineData(0.05f, 0.45f)]   // near broken: almost one in two
    [InlineData(0f, 1f)]         // broken: never bites
    public void A_dull_tool_glances_off_more_the_worse_it_is(float condition, float chance)
    {
        Assert.Equal(chance, HitDamage.GlanceChance(condition, _woodcutting), 3);
        Assert.Equal(chance, HitDamage.GlanceChance(condition, _mining), 3);
    }

    [Fact]
    public void Dull_below_0_turns_glancing_off_but_a_broken_tool_still_does_nothing()
    {
        var config = new WoodcuttingConfig { DullBelow = 0f };

        Assert.Equal(0f, HitDamage.GlanceChance(0.01f, config));
        Assert.Equal(1f, HitDamage.GlanceChance(0f, config));
    }

    [Fact]
    public void Every_axe_and_warhammer_knows_its_full_durability()
    {
        Assert.All(_woodcutting.Axes, axe => Assert.True(axe.Durability > 0, axe.Name));
        Assert.All(_mining.Hammers, hammer => Assert.True(hammer.Durability > 0, hammer.Name));
        Assert.Equal(168, _woodcutting.Axes.Single(axe => axe.Name == "Iron Battle Axe").Durability);
        Assert.Equal(784, _mining.Hammers.Single(hammer => hammer.Name == "Daedric Warhammer").Durability);
    }
}
