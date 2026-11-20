using System.Numerics;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Players;

public sealed class AwayRulesTests
{
    private static readonly Stance Still = new(new Vector3(100f, 200f, 300f), 90f);

    [Fact]
    public void Standing_still_with_the_network_jitter_is_not_a_stir()
    {
        Assert.False(AwayRules.HasStirred(Still, Still));
        Assert.False(AwayRules.HasStirred(Still, Still with { Position = Still.Position + new Vector3(AwayRules.MoveSlack - 1f, 0f, 0f) }));
        Assert.False(AwayRules.HasStirred(Still, Still with { Turn = Still.Turn + AwayRules.TurnSlack / 2 }));
    }

    [Fact]
    public void A_step_or_a_turn_of_the_camera_is_a_stir()
    {
        Assert.True(AwayRules.HasStirred(Still, Still with { Position = Still.Position + new Vector3(0f, AwayRules.MoveSlack + 1f, 0f) }));
        Assert.True(AwayRules.HasStirred(Still, Still with { Turn = Still.Turn + 15f }));
    }

    [Theory]
    [InlineData(359f, 1f, 2f)]
    [InlineData(1f, 359f, 2f)]
    [InlineData(-170f, 170f, 20f)]
    [InlineData(10f, 100f, 90f)]
    [InlineData(0f, 180f, 180f)]
    public void A_turn_is_measured_the_shorter_way_round(float from, float to, float expected) =>
        Assert.Equal(expected, AwayRules.TurnBetween(from, to), 3);

    [Fact]
    public void A_player_is_away_once_still_for_the_whole_time_and_not_before()
    {
        var after = TimeSpan.FromMinutes(5);

        Assert.False(AwayRules.IsAway(stirredAtMs: 0, nowMs: 299_999, after));
        Assert.True(AwayRules.IsAway(stirredAtMs: 0, nowMs: 300_000, after));
    }
}
