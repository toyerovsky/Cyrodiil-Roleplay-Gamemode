namespace CyrodiilRP.Server.Gambling;

internal sealed record GamblingView(long? Table, int MinStake, int MaxStake, int CutPercent, int RoundsToWin, int MaxReplays,
    int InviteSeconds, int DecisionSeconds, bool Rules, IReadOnlyList<GamblingOpponentView> Opponents, int? Opponent, GamblingMatchView? Match);
