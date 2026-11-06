using CyrodiilRP.Server.Infrastructure;
using ReadyM.Api.Idents;
using ReadyM.SDK.Server.Events;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Sanctuary;

// The area each player is in, from the server's own events ("L_ICMarketDistrict", or a shop's "L_PersistentDungeon-...").
// A character's scope can come back empty right after the player came in, so the events are the source. A player who
// disconnects leaves their area first, so nothing goes stale.
[Service]
internal sealed partial class PlayerAreas(IServerEvents events, Watchdog watchdog)
{
    private readonly Dictionary<PlayerId, string> _areas = [];

    // Null while the server has not said.
    public string? Find(PlayerId player) => _areas.GetValueOrDefault(player);

    private void Start()
    {
        events.OnPlayerJoinedArea += OnJoined;
        events.OnPlayerLeftArea += OnLeft;
    }

    private void OnJoined(PlayerId player, AreaId area) => watchdog.Run("sanctuary.area_joined", () => _areas[player] = area.ToManaged());

    private void OnLeft(PlayerId player, AreaId area) => watchdog.Run("sanctuary.area_left", () =>
    {
        if (_areas.TryGetValue(player, out var known) && known == area.ToManaged())
            _areas.Remove(player);
    });
}
