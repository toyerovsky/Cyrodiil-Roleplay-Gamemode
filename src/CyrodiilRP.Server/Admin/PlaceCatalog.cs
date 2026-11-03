using System.Text.Json;

namespace CyrodiilRP.Server.Admin;

// The places of /atp (places.json): the city gates, where the game puts a player leaving a city, and the map's cities,
// settlements and inns, all in the outside world.
internal sealed class PlaceCatalog(IReadOnlyList<Place> places)
{
    public static PlaceCatalog Shipped { get; } = Load(EmbeddedData.Read(EmbeddedData.Places));

    public IReadOnlyList<Place> All { get; } = places;

    public static PlaceCatalog Load(string json) =>
        new((JsonSerializer.Deserialize(json, AdminDataJson.Default.ListPlaceEntry) ?? [])
            .Select(entry => new Place(entry.Id, entry.Name, entry.Kind, GameSpace.ToUnreal(entry.X, entry.Y, entry.Z)))
            .ToList());

    public Found<Place> Find(string text) => CatalogLookup.Find(All, text, place => place.Id, place => place.Name);
}
