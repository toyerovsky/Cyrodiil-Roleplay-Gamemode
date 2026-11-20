namespace CyrodiilRP.Data.Gambling;

public sealed record GamblingPage(IReadOnlyList<GamblingGame> Games, bool More);
