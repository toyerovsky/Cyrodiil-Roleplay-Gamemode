namespace CyrodiilRP.Server.Gambling;

internal sealed record GamblingThrow(int Id, long Match, int Seat, int Round, int Replay, GamblingThrowKind Kind,
    int[] Indices, int[] Values, int Seed, string Set);
