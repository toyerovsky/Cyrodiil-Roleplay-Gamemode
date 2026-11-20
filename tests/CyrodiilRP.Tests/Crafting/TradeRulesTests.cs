using CyrodiilRP.Server.Crafting;

namespace CyrodiilRP.Tests.Crafting;

public sealed class TradeRulesTests
{
    [Theory]
    [InlineData(0, nameof(TradeRank.Novice))]
    [InlineData(TradeRules.ApprenticeLevel - 1, nameof(TradeRank.Novice))]
    [InlineData(TradeRules.ApprenticeLevel, nameof(TradeRank.Apprentice))]
    [InlineData(TradeRules.JourneymanLevel - 1, nameof(TradeRank.Apprentice))]
    [InlineData(TradeRules.JourneymanLevel, nameof(TradeRank.Journeyman))]
    [InlineData(TradeRules.ExpertLevel - 1, nameof(TradeRank.Journeyman))]
    [InlineData(TradeRules.ExpertLevel, nameof(TradeRank.Expert))]
    [InlineData(TradeRules.MaxLevel - 1, nameof(TradeRank.Expert))]
    [InlineData(TradeRules.MaxLevel, nameof(TradeRank.Master))]
    public void A_level_falls_in_one_of_five_ranks_and_master_is_the_last_level_alone(int level, string rank)
    {
        Assert.Equal(Enum.Parse<TradeRank>(rank), TradeRules.FindRank(level));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(TradeRules.MaxLevel + 1)]
    public void A_level_outside_the_ranks_has_no_rank(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TradeRules.FindRank(level));
    }
}
