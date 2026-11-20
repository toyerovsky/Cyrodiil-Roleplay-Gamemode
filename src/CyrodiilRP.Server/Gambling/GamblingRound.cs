namespace CyrodiilRP.Server.Gambling;

internal sealed record GamblingRound(int Number, int Replay, int[] FirstRoll, int[] SecondRoll, int[] FirstHand, int[] SecondHand, int Winner);
