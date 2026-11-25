using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Fishing;

// The players whose game was asked where the water is, each with when it was asked: an answer counts once, and only
// within FishingRules.LookAnswersWithin.
internal sealed class FishingLookRegistry
{
    private readonly Dictionary<OnlinePlayer, long> _askedAtMs = [];

    public void Ask(OnlinePlayer player, long now) => _askedAtMs[player] = now;

    // False for an answer nobody asked for, or one that came too late.
    public bool TryTake(OnlinePlayer player, long now) =>
        _askedAtMs.Remove(player, out var askedAt) && now - askedAt <= (long)FishingRules.LookAnswersWithin.TotalMilliseconds;

    public void Forget(OnlinePlayer player) => _askedAtMs.Remove(player);

    // Forgets the looks no answer came for in time, and hands back whose they were.
    public IReadOnlyList<OnlinePlayer> TakeUnanswered(long now)
    {
        var late = _askedAtMs
            .Where(pair => now - pair.Value > (long)FishingRules.LookAnswersWithin.TotalMilliseconds)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var player in late)
            _askedAtMs.Remove(player);

        return late;
    }
}
