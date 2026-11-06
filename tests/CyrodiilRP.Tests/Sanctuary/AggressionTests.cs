using CyrodiilRP.Server.Sanctuary;

namespace CyrodiilRP.Tests.Sanctuary;

public sealed class AggressionTests
{
    private const double Streak = 300;

    [Fact]
    public void A_first_attack_starts_a_streak()
    {
        var aggression = new Aggression();
        Assert.False(aggression.IsOn(10));
        Assert.True(aggression.Mark(10, Streak));
        Assert.True(aggression.IsOn(10));
        Assert.True(aggression.IsOn(10 + Streak - 1));
    }

    [Fact]
    public void Another_attack_in_the_streak_is_not_new_but_restarts_the_clock()
    {
        var aggression = new Aggression();
        aggression.Mark(10, Streak);
        Assert.False(aggression.Mark(200, Streak));
        Assert.True(aggression.IsOn(450));
        Assert.False(aggression.IsOn(500));
    }

    [Fact]
    public void The_end_of_a_streak_is_reported_once()
    {
        var aggression = new Aggression();
        aggression.Mark(0, Streak);
        Assert.False(aggression.Expired(Streak - 1));
        Assert.True(aggression.Expired(Streak));
        Assert.False(aggression.Expired(Streak + 1));
        Assert.False(aggression.IsOn(Streak + 1));
    }

    [Fact]
    public void An_attack_after_the_end_starts_a_new_streak()
    {
        var aggression = new Aggression();
        aggression.Mark(0, Streak);
        aggression.Expired(Streak);
        Assert.True(aggression.Mark(400, Streak));
    }

    [Fact]
    public void A_death_or_the_staff_clear_it_without_an_end_to_report()
    {
        var aggression = new Aggression();
        aggression.Mark(0, Streak);
        aggression.Clear();
        Assert.False(aggression.IsOn(1));
        Assert.False(aggression.Expired(1000));
        Assert.True(aggression.Mark(2, Streak));
    }
}
