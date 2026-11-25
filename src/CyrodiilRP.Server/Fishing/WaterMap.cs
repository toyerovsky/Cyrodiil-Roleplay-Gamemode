using System.Numerics;
using System.Text.Json;
using CyrodiilRP.Server.Admin;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Server.Fishing;

// The named waters of Tamriel (waters.json): the water a line goes into is the first whose outline holds the float and
// whose surface lies near the float's, so a highland pond inside a wide sea-level outline is not that water.
internal sealed class WaterMap(IReadOnlyList<MappedWater> waters)
{
    public const string Resource = "waters.json";

    // How far above or below a named water's surface the float's may lie and still be on it. Centimetres.
    public const float SameSurfaceWithin = 300f;

    private const int PointCoordinates = 2;
    private const int FewestPoints = 3;

    // The world spaces whose parent is Tamriel in Oblivion.esm: the cities and the Imperial City's districts stand at
    // their places in Tamriel, so its map holds there too.
    public static readonly IReadOnlyList<string> TamrielWorlds =
    [
        "Tamriel", "AnvilWorld", "AnvilCastleCourtyardWorld", "BravilWorld", "BrumaWorld", "CheydinhalWorld", "ChorrolWorld", "LeyawiinWorld",
        "SkingradWorld", "E3Kvatch", "KvatchEast", "KvatchEntrance", "KvatchPlaza", "ICArboretumDistrict", "ICArenaDistrict",
        "ICElvenGardensDistrict", "ICImperialPalace", "ICImperialPalaceMQ16", "ICImperialPrisonDistrict", "ICMarketDistrict",
        "ICTalosPlazaDistrict", "ICTempleDistrict", "ICTempleDistrictMQ16", "ICTheArcaneUniversity", "TGTempleOfTheEmperorZero",
    ];

    private static readonly HashSet<AreaId> MappedAreas = TamrielWorlds.Select(OblivionAreaIds.BuildExterior).ToHashSet();

    public static WaterMap Shipped { get; } = Load(EmbeddedData.Read(Resource));

    public IReadOnlyList<MappedWater> Waters { get; } = waters;

    public static WaterMap Load(string json)
    {
        var entries = JsonSerializer.Deserialize(json, FishingJson.Default.ListWaterEntry)
                      ?? throw new FormatException("The waters map is empty.");
        return new WaterMap(entries.Select(ToWater).ToList());
    }

    // Tamriel and the worlds standing in it by the map, any other area (an interior, Oblivion, the Shivering Isles) is
    // underground.
    public WaterKind Find(AreaId area, Vector3 point) =>
        MappedAreas.Contains(area) ? FindWater(point)?.Kind ?? WaterKind.Stream : WaterKind.Underground;

    // Null for water no named water holds. Z is the water's surface under the float.
    public MappedWater? FindWater(Vector3 point)
    {
        var flat = new Vector2(point.X, point.Y);
        return Waters.FirstOrDefault(water => Math.Abs(point.Z - water.SurfaceZ) <= SameSurfaceWithin && water.Holds(flat));
    }

    private static MappedWater ToWater(WaterEntry entry)
    {
        if (!Enum.IsDefined(entry.Kind) || entry.Kind is WaterKind.Stream or WaterKind.Underground)
            throw new FormatException($"The water {entry.Id} is of no kind a named water is: {entry.Kind}.");

        if (entry.Polygon.Length < FewestPoints || entry.Polygon.Any(point => point.Length != PointCoordinates || !point.All(float.IsFinite)))
            throw new FormatException($"The water {entry.Id} has no outline of points x, y.");

        return new MappedWater(entry.Id, entry.Name, entry.Kind, entry.SurfaceZ, entry.Polygon.Select(point => new Vector2(point[0], point[1])).ToList());
    }
}
