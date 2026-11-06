using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Sanctuary;

// The players the guardian watches, from the first tick it sees them until they leave. An aggressor's streak outlives the
// session: it is kept by the ReadyM account while the player is away, so reconnecting does not clear it.
internal sealed class PlayerWatchRegistry
{
    private readonly Dictionary<PlayerId, PlayerWatch> _watches = [];

    // ReadyM account to the end of the streak, on the guardian's clock.
    private readonly Dictionary<Guid, double> _awayUntil = [];

    public IReadOnlyDictionary<PlayerId, PlayerWatch> All => _watches;

    public bool TryGet(PlayerId player, out PlayerWatch watch) => _watches.TryGetValue(player, out watch!);

    // A player back before their streak ran out picks it up.
    public PlayerWatch Attach(PlayerId player, Guid account, double now)
    {
        if (_watches.TryGetValue(player, out var watch))
            return watch;

        _watches[player] = watch = new PlayerWatch(account);
        if (_awayUntil.Remove(account, out var until) && until > now)
            watch.Aggression.Resume(until);

        return watch;
    }

    // The watch the player left, null when there was none.
    public PlayerWatch? Detach(PlayerId player, double now)
    {
        if (!_watches.Remove(player, out var watch))
            return null;

        if (watch.Aggression.IsOn(now))
            _awayUntil[watch.Account] = watch.Aggression.Until;

        return watch;
    }

    public void ClearStreaks()
    {
        foreach (var watch in _watches.Values)
            watch.Aggression.Clear();

        _awayUntil.Clear();
    }
}
