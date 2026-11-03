using CyrodiilRP.Server.Infrastructure;

namespace CyrodiilRP.Server.Players;

// An ask of another player runs out unanswered after Timeout, and the asker waits Cooldown before asking again, so
// nobody is asked over and over. The texts show both numbers.
internal sealed class AskPacing(string name, Timers timers, IClock clock)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    private readonly Dictionary<OnlinePlayer, long> _askedAtMs = [];

    public static int TimeoutSeconds => RoundUpSeconds(Timeout);

    // Whole seconds the asker still waits, 0 when they may ask.
    public int CountWaitSeconds(OnlinePlayer asker) =>
        _askedAtMs.TryGetValue(asker, out var askedAt)
            ? Math.Max(0, RoundUpSeconds(Cooldown - TimeSpan.FromMilliseconds(clock.MonotonicMs - askedAt)))
            : 0;

    // The cooldown starts, and expire runs once the ask has gone unanswered for Timeout.
    public void Start(PlayerAsk ask, Action<PlayerAsk> expire)
    {
        _askedAtMs[ask.Asker] = clock.MonotonicMs;
        ask.Timeout = timers.RunAfter(name, Timeout, () => expire(ask));
    }

    public void Forget(OnlinePlayer player) => _askedAtMs.Remove(player);

    public static int RoundUpSeconds(TimeSpan span) => (int)Math.Ceiling(span.TotalSeconds);
}
