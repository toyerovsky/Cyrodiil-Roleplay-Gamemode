using System.Numerics;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Players;

// Distance only counts within one area: two areas can reuse the same coordinates.
[Service]
internal sealed partial class Proximity(IEntities entities)
{
    public bool TryLocate(PlayerId player, out AreaId area, out Vector3 position)
    {
        if (entities.TryLookup(player, out MainCharacter main) && entities.TryGetScope(main, out Area scope))
        {
            area = scope.AreaId;
            position = main.Position;
            return true;
        }

        area = default;
        position = default;
        return false;
    }

    // The player is included even when not in the world, so a speaker always reads their own line.
    public List<PlayerId> FindAround(PlayerId origin, float range)
    {
        var found = new List<PlayerId> { origin };
        if (!TryLocate(origin, out var area, out var position))
            return found;

        var rangeSquared = range * range;
        foreach (var main in entities.Query<MainCharacter>())
        {
            if (main.PlayerId == origin || !entities.TryGetScope(main, out Area scope) || scope.AreaId != area)
                continue;

            if (Vector3.DistanceSquared(main.Position, position) <= rangeSquared)
                found.Add(main.PlayerId);
        }

        return found;
    }

    public PlayerId? FindNearestInFront(PlayerId origin, IEnumerable<PlayerId> candidates, float reach, float coneDegrees)
    {
        if (!entities.TryLookup(origin, out MainCharacter main) || !entities.TryGetScope(main, out Area scope))
            return null;

        PlayerId? nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var candidate in candidates)
        {
            if (candidate == origin || !TryLocate(candidate, out var area, out var position) || area != scope.AreaId
                || !FrontCone.Contains(main.Position, main.Rotation, position, reach, coneDegrees))
                continue;

            var distance = Vector3.Distance(main.Position, position);
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    // Null in another area or out of the world.
    public float? MeasureDistance(PlayerId origin, AreaId area, Vector3 point) =>
        TryLocate(origin, out var originArea, out var position) && originArea == area ? Vector3.Distance(position, point) : null;

    public float? MeasureInFront(PlayerId origin, AreaId area, Vector3 point, float reach, float coneDegrees)
    {
        if (!entities.TryLookup(origin, out MainCharacter main) || !entities.TryGetScope(main, out Area scope) || scope.AreaId != area
            || !FrontCone.Contains(main.Position, main.Rotation, point, reach, coneDegrees))
            return null;

        return Vector3.Distance(main.Position, point);
    }

    public bool AreNear(PlayerId a, PlayerId b, float range)
        => TryLocate(a, out var areaA, out var positionA)
           && TryLocate(b, out var areaB, out var positionB)
           && areaA == areaB
           && Vector3.DistanceSquared(positionA, positionB) <= range * range;
}
