namespace CyrodiilRP.Server.Gambling;

internal static class GamblingTiming
{
    public const int RoundResultMs = 4000;

    public static long Deadline(long now, GamblingPhase phase, int decisionSeconds, bool rolling)
    {
        if (rolling) throw new InvalidOperationException("Decisions cannot start before the dice land.");
        return now + (phase == GamblingPhase.RoundResult ? RoundResultMs : decisionSeconds * 1000L);
    }

    // Nobody can decide while a game is being saved, so the time the save took is given back to the decision.
    public static long Resume(long expiresAt, long savingSince, long now) => expiresAt + Math.Max(0, now - savingSince);
}
