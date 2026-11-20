using System.Numerics;

namespace CyrodiilRP.Server.Players;

// Whether a player did anything between two looks: moved or turned the camera by more than the network's own jitter.
internal static class AwayRules
{
    // Game units, about centimetres.
    public const float MoveSlack = 10f;

    // Degrees.
    public const float TurnSlack = 1f;

    private const float FullTurn = 360f;

    private const float HalfTurn = 180f;

    public static bool HasStirred(Stance before, Stance now) =>
        Vector3.Distance(before.Position, now.Position) > MoveSlack || TurnBetween(before.Turn, now.Turn) > TurnSlack;

    // The shorter way round, so 359 to 1 is a turn of 2.
    public static float TurnBetween(float from, float to)
    {
        var turn = Math.Abs(to - from) % FullTurn;
        return turn > HalfTurn ? FullTurn - turn : turn;
    }

    public static bool IsAway(long stirredAtMs, long nowMs, TimeSpan after) => nowMs - stirredAtMs >= (long)after.TotalMilliseconds;
}
