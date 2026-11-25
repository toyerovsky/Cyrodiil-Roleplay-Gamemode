using System.Numerics;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Fishing;

// Where and when a line goes in: the water by the map, the hour by the world's clock and the weather of the area.
[Service]
internal sealed partial class FishingSurroundings(IEntities entities)
{
    public FishingConditions Read(Area area, Vector3 floatAt) =>
        new(WaterMap.Shipped.Find(area.AreaId, floatAt), FishingHours.Of(FindWorld().TimeOfDayMinutes), FishingWeathers.Of(area.WeatherFormId.Value));

    private World FindWorld()
    {
        foreach (var world in entities.Query<World>())
            return world;

        throw new InvalidOperationException("The server has no world while a player fishes.");
    }
}
