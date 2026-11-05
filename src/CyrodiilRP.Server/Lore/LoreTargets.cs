using System.Numerics;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Lore;

// Whose card a player means: the one in front, as the H wheel finds them, or those around for the card's Nearby.
[Service]
internal sealed partial class LoreTargets(OnlinePlayers online, Proximity proximity)
{
    // Twenty metres, as MESOL's Nearby.
    public const float NearbyRange = 2000f;

    private const float CentimetresPerMetre = 100f;

    public OnlinePlayer? FindInFront(OnlinePlayer actor) =>
        proximity.FindNearestInFront(actor.Id, WheelTargets.Filter(online.All).Select(p => p.Id), InteractionMenu.Reach, InteractionMenu.ConeDegrees) is { } id
        && online.TryGet(id, out var target)
            ? target
            : null;

    // The players in play around, nearest first.
    public IReadOnlyList<LoreNearby> ListNearby(OnlinePlayer actor)
    {
        if (!proximity.TryLocate(actor.Id, out var area, out var position))
            return [];

        List<LoreNearby> found = [];
        foreach (var other in WheelTargets.Filter(online.All))
        {
            if (other == actor || other.Character is null || !proximity.TryLocate(other.Id, out var otherArea, out var otherPosition) || otherArea != area)
                continue;

            var distance = Vector3.Distance(position, otherPosition);
            if (distance <= NearbyRange)
                found.Add(new LoreNearby(other, distance / CentimetresPerMetre));
        }

        return found.OrderBy(nearby => nearby.Metres).ToList();
    }

    public static double RangeMetres => NearbyRange / CentimetresPerMetre;
}
