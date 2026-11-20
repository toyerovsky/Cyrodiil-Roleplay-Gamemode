using CyrodiilRP.Data.Gambling;

namespace CyrodiilRP.Server.Gambling;

internal sealed record GamblingHistoryView(long Id, long At, GamblingMode Mode, string First, string Second, string Opponent,
    int Stake, int Pot, GamblingStatus Status, string? Winner, int Cut);
