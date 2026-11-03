using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Characters;

// One player holds each name, whatever its letter case. In memory, so a restart frees them all: the database's unique
// index stays the last word.
internal sealed class NameReservations
{
    private readonly Dictionary<string, OnlinePlayer> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<OnlinePlayer, ReservedName> _byPlayer = [];

    public ReservedName? Find(OnlinePlayer player) => _byPlayer.GetValueOrDefault(player);

    // False when another player holds the name. A player giving another name lets go of the one before.
    public bool TryReserve(OnlinePlayer player, ReservedName name)
    {
        if (_byKey.TryGetValue(name.Key, out var holder) && holder != player)
            return false;

        Release(player);
        _byKey[name.Key] = player;
        _byPlayer[player] = name;
        return true;
    }

    public void Release(OnlinePlayer player)
    {
        if (_byPlayer.Remove(player, out var name))
            _byKey.Remove(name.Key);
    }
}
