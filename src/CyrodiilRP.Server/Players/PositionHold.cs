using OblivionMP.SDK.Common.Archetypes;
using ReadyM.Api.Idents;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Players;

// Puts back a character that moved anyway, pushed or by a game that did not listen (HeldSpot).
internal sealed class PositionHold(Proximity proximity, IEntities entities)
{
    private readonly Dictionary<PlayerId, HeldSpot> _spots = [];

    public void Release(PlayerId id) => _spots.Remove(id);

    public void Keep(PlayerId id)
    {
        if (!proximity.TryLocate(id, out var area, out var position))
            return;

        if (!_spots.TryGetValue(id, out var spot))
            _spots[id] = new HeldSpot(area, position);
        else if (spot.Check(area, position) is { } back && entities.TryLookup(id, out MainCharacter main))
            main.Position = back;
    }
}
