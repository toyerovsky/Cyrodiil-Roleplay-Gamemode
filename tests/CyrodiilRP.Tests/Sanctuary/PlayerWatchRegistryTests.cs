using CyrodiilRP.Server.Sanctuary;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Tests.Sanctuary;

public sealed class PlayerWatchRegistryTests
{
    private const double Streak = 300;

    // Two sessions: the SDK makes no other ids outside the server.
    private static readonly PlayerId First = PlayerId.Server;
    private static readonly PlayerId Again = PlayerId.Invalid;

    [Fact]
    public void An_aggressor_who_reconnects_keeps_the_streak()
    {
        var registry = new PlayerWatchRegistry();
        var account = Guid.NewGuid();
        registry.Attach(First, account, 0).Aggression.Mark(10, Streak);

        Assert.NotNull(registry.Detach(First, 20));
        var back = registry.Attach(Again, account, 40);

        Assert.True(back.Aggression.IsOn(40));
        Assert.Equal(10 + Streak, back.Aggression.Until);
    }

    [Fact]
    public void A_streak_that_ran_out_while_away_is_gone()
    {
        var registry = new PlayerWatchRegistry();
        var account = Guid.NewGuid();
        registry.Attach(First, account, 0).Aggression.Mark(10, Streak);
        registry.Detach(First, 20);

        Assert.False(registry.Attach(Again, account, 10 + Streak + 1).Aggression.IsOn(10 + Streak + 1));
    }

    [Fact]
    public void Another_account_does_not_pick_up_the_streak()
    {
        var registry = new PlayerWatchRegistry();
        registry.Attach(First, Guid.NewGuid(), 0).Aggression.Mark(10, Streak);
        registry.Detach(First, 20);

        Assert.False(registry.Attach(Again, Guid.NewGuid(), 30).Aggression.IsOn(30));
    }

    [Fact]
    public void Clearing_the_streaks_ends_those_online_and_away()
    {
        var registry = new PlayerWatchRegistry();
        var away = Guid.NewGuid();
        registry.Attach(First, away, 0).Aggression.Mark(10, Streak);
        registry.Detach(First, 20);
        var online = registry.Attach(Again, Guid.NewGuid(), 20);
        online.Aggression.Mark(20, Streak);

        registry.ClearStreaks();

        Assert.False(online.Aggression.IsOn(21));
        Assert.False(registry.Attach(First, away, 22).Aggression.IsOn(22));
    }

    [Fact]
    public void A_player_never_seen_leaves_nothing_behind()
    {
        Assert.Null(new PlayerWatchRegistry().Detach(First, 0));
    }
}
