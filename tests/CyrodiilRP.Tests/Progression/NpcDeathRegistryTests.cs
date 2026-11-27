using CyrodiilRP.Server.Progression;

namespace CyrodiilRP.Tests.Progression;

public sealed class NpcDeathRegistryTests
{
    [Fact]
    public void The_first_accepted_player_damage_starts_the_clock_and_death_is_paid_once()
    {
        var deaths = new NpcDeathRegistry();
        deaths.Before(default, 100);
        Assert.False(deaths.Observe(default, true, 70, 10, out var first));
        Assert.Equal(10, first);
        deaths.Before(default, 70);
        Assert.True(deaths.Observe(default, true, 0, 30, out first));
        Assert.Equal(10, first);
        deaths.Before(default, 0);
        Assert.False(deaths.Observe(default, true, 0, 40, out _));
    }

    [Fact]
    public void A_lethal_first_hit_has_zero_ttk_and_a_dead_npc_cannot_start_a_death()
    {
        var deaths = new NpcDeathRegistry();
        deaths.Before(default, 0);
        Assert.False(deaths.Observe(default, true, 0, 1, out _));
        deaths.Before(default, 100);
        Assert.True(deaths.Observe(default, true, 0, 2, out var first));
        Assert.Equal(2, first);
    }

    [Fact]
    public void Rejected_damage_does_not_start_ttk_and_a_revived_npc_gets_a_new_lifetime()
    {
        var deaths = new NpcDeathRegistry();
        deaths.Before(default, 100);
        // A rejected event never reaches Observe.
        deaths.Before(default, 100);
        Assert.True(deaths.Observe(default, true, 0, 20, out var first));
        Assert.Equal(20, first);
        deaths.Before(default, 100);
        Assert.True(deaths.Observe(default, true, 0, 40, out first));
        Assert.Equal(40, first);
    }

    [Fact]
    public void An_environmental_kill_is_recorded_without_player_attribution()
    {
        var deaths = new NpcDeathRegistry();
        deaths.Before(default, 100);
        Assert.True(deaths.Observe(default, false, 0, 20, out var first));
        Assert.Null(first);
        deaths.Before(default, 0);
        Assert.False(deaths.Observe(default, true, 0, 30, out _));
    }
}
