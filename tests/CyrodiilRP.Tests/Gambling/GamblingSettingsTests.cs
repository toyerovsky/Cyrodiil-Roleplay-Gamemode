using CyrodiilRP.Server;

namespace CyrodiilRP.Tests.Gambling;

public sealed class GamblingSettingsTests
{
    [Fact]
    public void The_defaults_form_valid_rules() => new GamblingSettings().Check();

    [Theory]
    [InlineData("MinStake", 0)]
    [InlineData("MaxStake", 0)]
    [InlineData("CutPercent", -1)]
    [InlineData("CutPercent", 51)]
    [InlineData("GamesPerTable", 0)]
    [InlineData("InviteSeconds", 0)]
    [InlineData("DecisionSeconds", 0)]
    [InlineData("PokerRoundsToWin", 0)]
    [InlineData("MaxReplays", -1)]
    [InlineData("MaxStake", int.MaxValue)]
    public void Impossible_rules_stop_startup(string key, int value)
    {
        var settings = new GamblingSettings();
        typeof(GamblingSettings).GetProperty(key)!.SetValue(settings, value);
        Assert.Contains("gambling", Assert.Throws<InvalidOperationException>(settings.Check).Message);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Reach_must_be_positive_and_finite(float reach) => Assert.Throws<InvalidOperationException>(new GamblingSettings { TableReach = reach }.Check);
}
