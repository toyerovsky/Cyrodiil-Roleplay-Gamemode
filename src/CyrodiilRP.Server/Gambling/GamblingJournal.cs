namespace CyrodiilRP.Server.Gambling;

internal sealed record GamblingJournal(IReadOnlyList<GamblingRound> Rounds, int Round, int Replay, GamblingPhase Phase,
    int[] FirstHand, int[] SecondHand, int[] Wins, int Turn, int PendingRaise);
