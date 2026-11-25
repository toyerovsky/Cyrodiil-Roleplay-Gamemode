using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Admin;

// Who flies, and who landed a moment ago: a flyer takes no harm, and none for LandingGrace after, which is the fall
// from where the flight ended.
internal sealed class FlightRegistry(IClock clock)
{
    public static readonly TimeSpan LandingGrace = TimeSpan.FromSeconds(15);

    private readonly HashSet<OnlinePlayer> _flying = [];
    private readonly Dictionary<OnlinePlayer, DateTimeOffset> _landed = [];

    public bool IsFlying(OnlinePlayer player) => _flying.Contains(player);

    public void TakeOff(OnlinePlayer player)
    {
        _flying.Add(player);
        _landed.Remove(player);
    }

    // False for a player who was not flying.
    public bool Land(OnlinePlayer player)
    {
        if (!_flying.Remove(player))
            return false;

        _landed[player] = clock.UtcNow + LandingGrace;
        return true;
    }

    public bool Spares(OnlinePlayer player)
    {
        if (_flying.Contains(player))
            return true;

        if (!_landed.TryGetValue(player, out var until))
            return false;

        if (clock.UtcNow < until)
            return true;

        _landed.Remove(player);
        return false;
    }

    public void Forget(OnlinePlayer player)
    {
        _flying.Remove(player);
        _landed.Remove(player);
    }
}
