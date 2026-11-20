namespace CyrodiilRP.Server;

internal sealed class GamblingSettings
{
    public int MinStake { get; set; } = 1;
    public int MaxStake { get; set; } = 500;
    public int CutPercent { get; set; }
    public float TableReach { get; set; } = 300f;
    public int GamesPerTable { get; set; } = 2;
    public int InviteSeconds { get; set; } = 30;
    public int DecisionSeconds { get; set; } = 45;
    public int PokerRoundsToWin { get; set; } = 2;
    public int MaxReplays { get; set; } = 3;

    public void Check()
    {
        if (MinStake < 1 || MaxStake < MinStake || CutPercent is < 0 or > 50)
            throw new InvalidOperationException("gambling in config.json: minStake must be positive, maxStake at least minStake, cutPercent from 0 to 50.");

        if (!float.IsFinite(TableReach) || TableReach <= 0 || GamesPerTable < 1 || InviteSeconds < 1 || DecisionSeconds < 1
            || PokerRoundsToWin < 1 || MaxReplays < 0)
            throw new InvalidOperationException("gambling in config.json: tableReach, gamesPerTable, inviteSeconds, decisionSeconds and pokerRoundsToWin must be positive, maxReplays nonnegative.");

        var maximumPot = 2m * MaxStake * (1m + 2m * (2m * PokerRoundsToWin - 1m));
        if (maximumPot > int.MaxValue)
            throw new InvalidOperationException("gambling in config.json: maxStake and pokerRoundsToWin would overflow the gold pot.");
    }
}
