namespace CyrodiilRP.Data.Gambling;

public sealed record GamblingHistory(IReadOnlyList<GamblingGame> Games, IReadOnlyList<long> KnownNames);
