namespace CyrodiilRP.Server.Gambling;

internal sealed record GamblingDecision(long Game, int Version, string Action, int Amount);
