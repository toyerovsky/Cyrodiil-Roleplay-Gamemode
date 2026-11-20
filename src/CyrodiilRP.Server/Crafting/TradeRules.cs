namespace CyrodiilRP.Server.Crafting;

internal static class TradeRules
{
    public const int MaxLevel = 100;
    public const int ApprenticeLevel = 25;
    public const int JourneymanLevel = 50;
    public const int ExpertLevel = 75;

    public static TradeRank FindRank(int level) => level switch
    {
        < 0 or > MaxLevel => throw new ArgumentOutOfRangeException(nameof(level), level, $"A trade's level is 0 to {MaxLevel}."),
        < ApprenticeLevel => TradeRank.Novice,
        < JourneymanLevel => TradeRank.Apprentice,
        < ExpertLevel => TradeRank.Journeyman,
        < MaxLevel => TradeRank.Expert,
        MaxLevel => TradeRank.Master,
    };
}
