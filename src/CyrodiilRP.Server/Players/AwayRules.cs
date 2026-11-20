using System.Numerics;

namespace CyrodiilRP.Server.Players;

// Whether a player did anything between two looks (moved or turned the camera by more than the network's own jitter),
// and how far a player who did nothing is from the mark.
internal static class AwayRules
{
    // Game units, about centimetres.
    public const float MoveSlack = 10f;

    // Degrees.
    public const float TurnSlack = 1f;

    // The screen asks this long before the mark, so a listener who looks up is never marked.
    public static readonly TimeSpan WarnFor = TimeSpan.FromSeconds(30);

    // A sign this soon after /afk is the hand leaving the keys, not a return.
    public static readonly TimeSpan ManualGrace = TimeSpan.FromSeconds(2);

    private const float FullTurn = 360f;

    private const float HalfTurn = 180f;

    private const double MsPerSecond = 1000;

    public static bool HasStirred(Stance before, Stance now) =>
        Vector3.Distance(before.Position, now.Position) > MoveSlack || TurnBetween(before.Turn, now.Turn) > TurnSlack;

    // The shorter way round, so 359 to 1 is a turn of 2.
    public static float TurnBetween(float from, float to)
    {
        var turn = Math.Abs(to - from) % FullTurn;
        return turn > HalfTurn ? FullTurn - turn : turn;
    }

    public static bool IsAway(long stirredAtMs, long nowMs, TimeSpan after) => nowMs - stirredAtMs >= (long)after.TotalMilliseconds;

    // Never asks before half the time is gone, for a server that marks after only a minute.
    public static AwayStage StageAfter(long stirredAtMs, long nowMs, TimeSpan after)
    {
        if (IsAway(stirredAtMs, nowMs, after))
            return AwayStage.Away;

        var warning = TimeSpan.FromTicks(Math.Min(WarnFor.Ticks, after.Ticks / 2));
        return IsAway(stirredAtMs, nowMs, after - warning) ? AwayStage.Warned : AwayStage.Here;
    }

    // Rounded up, so the countdown never shows 0 while the mark is still to come.
    public static int SecondsLeft(long stirredAtMs, long nowMs, TimeSpan after) =>
        Math.Max(0, (int)Math.Ceiling((stirredAtMs + after.TotalMilliseconds - nowMs) / MsPerSecond));

    public static int SecondsSince(long sinceMs, long nowMs) => (int)Math.Max(0, (nowMs - sinceMs) / (long)MsPerSecond);
}
