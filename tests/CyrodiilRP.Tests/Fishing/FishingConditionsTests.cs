using System.Numerics;
using System.Text.Json;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Tests.Fakes;
using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Tests.Fishing;

public sealed class FishingConditionsTests
{
    // The research's points (water-points.json), x and y in the server's centimetres, the water's surface under each and
    // the named water it must fall in, or none.
    private static readonly string PointsFile = Path.Combine(Repository.Root, "tests", "CyrodiilRP.Tests", "Fishing", "water-points.json");

    private static readonly Dictionary<string, WaterKind> KindsByWater = new(StringComparer.Ordinal)
    {
        ["lake-rumare"] = WaterKind.LakeRumare,
        ["niben-bay"] = WaterKind.NibenBay,
        ["topal-bay"] = WaterKind.Sea,
        ["abecean-sea"] = WaterKind.Sea,
        ["niben"] = WaterKind.River,
        ["niben-rivers"] = WaterKind.River,
        ["west-rivers"] = WaterKind.River,
        ["lake-arrius"] = WaterKind.Lake,
        ["lake-poppad"] = WaterKind.Lake,
        ["lake-canulus"] = WaterKind.Lake,
    };

    public static TheoryData<string> PointNames() => [.. ReadPoints().Select(point => point.Name)];

    [Theory]
    [MemberData(nameof(PointNames))]
    public void Each_of_the_researchs_points_falls_in_its_water_or_in_none(string name)
    {
        var point = ReadPoints().Single(point => point.Name == name);

        var found = WaterMap.Shipped.FindWater(new Vector3(point.X, point.Y, point.SurfaceZ));

        Assert.Equal(point.Expect, found?.Id);
        Assert.Equal(point.Expect is { } id ? KindsByWater[id] : WaterKind.Stream, WaterMap.Shipped.Find(GameSpace.Outside, new Vector3(point.X, point.Y, point.SurfaceZ)));
    }

    [Fact]
    public void Every_named_water_is_shipped_with_its_kind_and_none_is_a_stream_or_underground()
    {
        Assert.Equal(KindsByWater.Keys.Order(), WaterMap.Shipped.Waters.Select(water => water.Id).Order());
        Assert.All(WaterMap.Shipped.Waters, water => Assert.Equal(KindsByWater[water.Id], water.Kind));
    }

    [Fact]
    public void A_city_standing_in_tamriel_uses_its_map_and_an_interior_or_another_world_is_underground()
    {
        var bravil = new Vector3(94717, 45037, 0);

        Assert.Equal(WaterKind.NibenBay, WaterMap.Shipped.Find(OblivionAreaIds.BuildExterior("BravilWorld"), bravil));
        Assert.Equal(WaterKind.Underground, WaterMap.Shipped.Find(OblivionAreaIds.BuildInterior(0x2F7D5), bravil));
        Assert.Equal(WaterKind.Underground, WaterMap.Shipped.Find(OblivionAreaIds.BuildExterior("SEWorld"), bravil));
    }

    [Fact]
    public void A_pond_high_above_a_wide_waters_surface_is_not_that_water()
    {
        var rumare = new Vector3(11703, -81920, 0);

        Assert.Equal(WaterKind.LakeRumare, WaterMap.Shipped.Find(GameSpace.Outside, rumare with { Z = WaterMap.SameSurfaceWithin }));
        Assert.Equal(WaterKind.Stream, WaterMap.Shipped.Find(GameSpace.Outside, rumare with { Z = WaterMap.SameSurfaceWithin + 1 }));
    }

    [Theory]
    [InlineData("[{\"id\": \"pond\", \"name\": \"Pond\", \"kind\": \"Stream\", \"surfaceZ\": 0, \"polygon\": [[0, 0], [1, 0], [1, 1]]}]")]
    [InlineData("[{\"id\": \"pond\", \"name\": \"Pond\", \"kind\": \"Lake\", \"surfaceZ\": 0, \"polygon\": [[0, 0], [1, 0]]}]")]
    [InlineData("[{\"id\": \"pond\", \"name\": \"Pond\", \"kind\": \"Lake\", \"surfaceZ\": 0, \"polygon\": [[0, 0], [1, 0], [1]]}]")]
    public void A_map_with_a_water_of_no_named_kind_or_no_outline_is_refused(string json)
    {
        Assert.Throws<FormatException>(() => WaterMap.Load(json));
    }

    [Fact]
    public void A_small_map_holds_a_point_inside_its_outline_and_not_one_outside()
    {
        var map = WaterMap.Load("[{\"id\": \"pond\", \"name\": \"Pond\", \"kind\": \"Lake\", \"surfaceZ\": 100, \"polygon\": [[0, 0], [1000, 0], [1000, 1000], [500, 400], [0, 1000]]}]");

        Assert.Equal("pond", map.FindWater(new Vector3(500, 200, 100))?.Id);
        Assert.Null(map.FindWater(new Vector3(500, 800, 100)));
        Assert.Null(map.FindWater(new Vector3(-1, 200, 100)));
    }

    [Theory]
    [InlineData(0f, "Night")]
    [InlineData(FishingHours.DawnFrom - 1, "Night")]
    [InlineData(FishingHours.DawnFrom, "Dawn")]
    [InlineData(FishingHours.DayFrom, "Day")]
    [InlineData(FishingHours.DuskFrom - 1, "Day")]
    [InlineData(FishingHours.DuskFrom, "Dusk")]
    [InlineData(FishingHours.NightFrom, "Night")]
    [InlineData(1439.9f, "Night")]
    [InlineData(1440f, "Night")]
    public void The_hour_of_the_worlds_clock_is_a_part_of_the_day(float minutes, string part)
    {
        Assert.Equal(Enum.Parse<DayPart>(part), FishingHours.Of(minutes));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(1441f)]
    [InlineData(float.NaN)]
    public void A_time_of_day_outside_a_day_is_refused(float minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FishingHours.Of(minutes));
    }

    [Fact]
    public void Every_plain_weather_is_a_weather_the_fish_know_and_any_other_form_changes_nothing()
    {
        foreach (var name in WeatherCatalog.Shipped.Names)
        {
            Assert.True(WeatherCatalog.Shipped.TryFind(name, out var form));
            Assert.Equal(FishingWeathers.Parse(name), FishingWeathers.Of(form));
            Assert.NotEqual(WeatherKind.Other, FishingWeathers.Of(form));
        }

        Assert.Equal(Enum.GetValues<WeatherKind>().Where(kind => kind != WeatherKind.Other).Order(), WeatherCatalog.Shipped.Names.Select(FishingWeathers.Parse).Order());
        Assert.Equal(WeatherKind.Other, FishingWeathers.Of(0));
        Assert.Throws<InvalidOperationException>(() => FishingWeathers.Parse("sandstorm"));
    }

    private static List<WaterPoint> ReadPoints()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PointsFile));
        return document.RootElement.EnumerateArray()
            .Select(point => new WaterPoint(
                point.GetProperty("name").GetString()!,
                point.GetProperty("x").GetSingle(),
                point.GetProperty("y").GetSingle(),
                point.GetProperty("surfaceZ").GetSingle(),
                point.GetProperty("expect").GetString()))
            .ToList();
    }

    private sealed record WaterPoint(string Name, float X, float Y, float SurfaceZ, string? Expect);
}
