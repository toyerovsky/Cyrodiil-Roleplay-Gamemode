using CyrodiilRP.Server.Party;

namespace CyrodiilRP.Tests.Party;

public sealed class PartyRulesTests
{
    private const float HealthMax = 120f;

    [Theory]
    [InlineData(120f, 100)]
    [InlineData(61f, 55)]
    [InlineData(0.5f, PartyRules.HealthStep)]
    [InlineData(0f, 0)]
    [InlineData(-3f, 0)]
    [InlineData(500f, 100)]
    public void Health_shows_in_steps_rounded_up_so_only_the_dead_show_none(float health, int percent) =>
        Assert.Equal(percent, PartyRules.ComputeHealthPercent(health, HealthMax));

    [Fact]
    public void A_character_whose_maximum_the_game_has_not_said_shows_no_health() =>
        Assert.Null(PartyRules.ComputeHealthPercent(HealthMax, 0f));
}
