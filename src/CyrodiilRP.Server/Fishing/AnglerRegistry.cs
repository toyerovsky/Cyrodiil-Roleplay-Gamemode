using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Fishing;

// The players fishing now, one line each.
internal sealed class AnglerRegistry
{
    private readonly Dictionary<OnlinePlayer, Angler> _anglers = [];

    public bool Contains(OnlinePlayer player) => _anglers.ContainsKey(player);

    public bool TryGet(OnlinePlayer player, out Angler angler) => _anglers.TryGetValue(player, out angler!);

    // Adds one, or puts the new state of one in its place.
    public void Set(Angler angler) => _anglers[angler.Player] = angler;

    public bool Remove(OnlinePlayer player, out Angler angler) => _anglers.Remove(player, out angler!);

    // A copy, so a caller may set and remove while it goes through them.
    public IReadOnlyList<Angler> ListAll() => [.. _anglers.Values];
}
