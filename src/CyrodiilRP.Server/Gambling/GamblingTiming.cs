namespace CyrodiilRP.Server.Gambling;

internal static class GamblingTiming
{
    public const int RoundResultMs = 4000;

    public static long Deadline(long now, GamblingPhase phase, int decisionSeconds, bool rolling)
    {
        if (rolling) throw new InvalidOperationException("Decisions cannot start before the dice land.");
        return now + (phase == GamblingPhase.RoundResult ? RoundResultMs : decisionSeconds * 1000L);
    }
}
