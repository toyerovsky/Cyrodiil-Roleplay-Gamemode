using CyrodiilRP.Server.Sanctuary;

namespace CyrodiilRP.Tests.Sanctuary;

public sealed class ZoneTests
{
    private const string Market = "L_ICMarketDistrict";
    private const string Bloodworks = "L_PersistentDungeon-37868";
    private const string Prison = "L_PersistentDungeon-2D941";
    private const string ArenaFloor = "L_PersistentDungeon-37867";
    private const string ArenaStands = "L_PersistentDungeon-1C60F";
    private const string ArenaSpectators = "L_PersistentDungeon-3D61C";
    private const string MarketSewers = "L_PersistentDungeon-96AC5";
    private const string Tamriel = "L_Tamriel";
    private const int Districts = 11;
    private const int ArenaCells = 5;

    private static readonly Zone City = new([], []);

    [Theory]
    [InlineData(Market)]
    [InlineData("L_ICTempleDistrict")]
    [InlineData("L_ICArenaDistrict")]
    [InlineData("L_ICTheArcaneUniversity")]
    [InlineData(Bloodworks)]
    [InlineData(Prison)]
    public void The_city_inside_the_walls_is_the_sanctuary(string area) => Assert.True(City.Contains(area));

    [Theory]
    [InlineData(ArenaFloor)]
    [InlineData(ArenaStands)]
    [InlineData(ArenaSpectators)]
    [InlineData(Tamriel)]
    [InlineData("L_ChorrolWorld")]
    [InlineData(null)]
    public void The_arena_and_the_rest_of_cyrodiil_are_not(string? area) => Assert.False(City.Contains(area));

    [Fact]
    public void The_sewers_under_the_city_are_not()
    {
        Assert.DoesNotContain(ImperialCity.Interiors, area => area.EditorId.Contains("Sewer", StringComparison.Ordinal));
        Assert.False(City.Contains(MarketSewers));
    }

    [Fact]
    public void The_config_adds_and_removes_areas()
    {
        var zone = new Zone([Tamriel], [Market]);
        Assert.True(zone.Contains(Tamriel));
        Assert.False(zone.Contains(Market));
        Assert.Equal(City.Count, zone.Count);
    }

    [Fact]
    public void The_arena_cannot_be_added()
    {
        var zone = new Zone([ArenaFloor, ArenaSpectators], []);
        Assert.False(zone.Contains(ArenaFloor));
        Assert.False(zone.Contains(ArenaSpectators));
    }

    [Fact]
    public void Areas_have_their_names()
    {
        Assert.Equal("Market District", City.NameOf(Market));
        Assert.Equal("Arena Bloodworks", City.NameOf(Bloodworks));
        Assert.Equal("Imperial City Arena", City.NameOf(ArenaFloor));
        Assert.Equal(Tamriel, City.NameOf(Tamriel));
    }

    [Fact]
    public void The_city_list_has_no_duplicates()
    {
        var all = ImperialCity.Districts.Concat(ImperialCity.Interiors).Concat(ImperialCity.Arena).Select(area => area.Id).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(Districts, ImperialCity.Districts.Length);
        Assert.Equal(ArenaCells, ImperialCity.Arena.Length);
    }
}
