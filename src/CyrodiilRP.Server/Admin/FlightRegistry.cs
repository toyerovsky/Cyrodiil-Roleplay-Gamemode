using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Admin;

// Who flies, and who a flight left in the air: a flyer takes no harm, and none for LandingGrace once the fall from
// where the flight ended begins.
internal sealed class FlightRegistry(IClock clock)
{
    public static readonly TimeSpan LandingGrace = TimeSpan.FromSeconds(15);

    private readonly Dictionary<OnlinePlayer, Flight> _flights = [];

    // SparedUntil: null for as long as the character does not fall, flying or held where the flight ended.
    private sealed record Flight(bool IsFlying, DateTimeOffset? SparedUntil);

    public bool IsFlying(OnlinePlayer player) => _flights.GetValueOrDefault(player) is { IsFlying: true };

    public void TakeOff(OnlinePlayer player) => _flights[player] = new Flight(IsFlying: true, SparedUntil: null);

    // isHeld: the character stays where the flight ended, as under a freeze, so its fall begins with Drop.
    public void Land(OnlinePlayer flyer, bool isHeld) =>
        _flights[flyer] = new Flight(IsFlying: false, isHeld ? null : clock.UtcNow + LandingGrace);

    public void Drop(OnlinePlayer player)
    {
        if (_flights.GetValueOrDefault(player) is { IsFlying: false, SparedUntil: null })
            Land(player, isHeld: false);
    }

    public bool Spares(OnlinePlayer player)
    {
        if (!_flights.TryGetValue(player, out var flight))
            return false;

        if (flight.SparedUntil is not { } until || clock.UtcNow < until)
            return true;

        _flights.Remove(player);
        return false;
    }

    public void Forget(OnlinePlayer player) => _flights.Remove(player);
}
