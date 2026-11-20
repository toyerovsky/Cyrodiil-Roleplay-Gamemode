using CyrodiilRP.Data.Gambling;

namespace CyrodiilRP.Server.Gambling;

internal sealed record GamblingMatchView(long Id, int Version, GamblingMode Mode, GamblingPhase Phase, bool Busy, int Stake, int Pot,
    int Round, int Replay, int[] Wins, string You, string Opponent, int[] YourHand, int[] OpponentHand, string YourRank,
    string OpponentRank, bool YourTurn, bool Confirmed, int RaiseLimit, int CallAmount, long LeftMs, int Winner, string? Result,
    int Seat, int TurnSeat, int[] Picks, GamblingThrow[] Throws, int? ActiveThrow, long ThrowElapsedMs, int AnimationMs, int RoundWinner);
