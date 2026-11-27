using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Party;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Zones;
using CyrodiilRP.Tests.Fakes;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Tests.Zones;

public sealed class ZoneTests
{
    private static ZoneSettings Shipped()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var config = JsonSerializer.Deserialize<RpConfig>(File.ReadAllText(Path.Combine(Repository.Root, "src", "CyrodiilRP.Server", "config.json")), options)!;
        config.Zones.RequireValid();
        return config.Zones;
    }

    private static ZoneCell Outside(int x, int y) => new(GameSpace.Outside, ParentCellKind.Exterior, x, y);

    [Fact]
    public void An_entity_scope_supplies_the_NPC_cell_including_negative_coordinates()
    {
        Assert.Equal(Outside(-48, -9), ZoneCell.From(new FullCellId(GameSpace.Outside, OblivionCellIds.Exterior(-48, -9))));
        var interior = OblivionAreaIds.BuildInterior(1);
        Assert.Equal(new ZoneCell(interior, ParentCellKind.Interior, 0, 0), ZoneCell.From(new FullCellId(interior, OblivionCellIds.Interior)));
        Assert.Throws<FormatException>(() => ZoneCell.From(new FullCellId(GameSpace.Outside, new CellId("invalid"))));
    }

    [Theory]
    [InlineData(4, 15, "imperial-city")]
    [InlineData(0, 15, "heartlands")]
    [InlineData(-1, 15, "heartlands")]
    [InlineData(4, 10, "imperial-city")]
    [InlineData(27, 22, "blue-road")]
    [InlineData(30, 23, "blue-road")]
    [InlineData(29, 19, "cheydinhal")]
    [InlineData(30, 10, "cheydinhal")]
    [InlineData(18, 20, "blue-road")]
    [InlineData(11, 25, "heartlands")]
    [InlineData(3, 25, "heartlands")]
    [InlineData(15, 0, "heartlands")]
    [InlineData(-27, 20, "colovian-highlands")]
    [InlineData(-25, 30, "colovian-highlands")]
    [InlineData(5, 35, "jerall")]
    [InlineData(6, 35, "jerall")]
    [InlineData(5, 36, "jerall")]
    [InlineData(6, 36, "jerall")]
    [InlineData(3, 39, "jerall")]
    [InlineData(6, 29, "great-forest")]
    [InlineData(29, 31, "cheydinhal")]
    [InlineData(-17, 22, "great-forest")]
    [InlineData(-16, 22, "great-forest")]
    [InlineData(-17, 23, "great-forest")]
    [InlineData(-16, 23, "great-forest")]
    [InlineData(-17, 24, "great-forest")]
    [InlineData(-16, 24, "great-forest")]
    [InlineData(-14, 20, "great-forest")]
    [InlineData(-17, 15, "great-forest")]
    [InlineData(-19, 13, "great-forest")]
    [InlineData(-11, 10, "great-forest")]
    [InlineData(-18, -1, "west-weald")]
    [InlineData(-17, -1, "west-weald")]
    [InlineData(-16, -1, "west-weald")]
    [InlineData(-18, 0, "west-weald")]
    [InlineData(-17, 0, "west-weald")]
    [InlineData(-16, 0, "west-weald")]
    [InlineData(-18, 1, "west-weald")]
    [InlineData(-17, 1, "west-weald")]
    [InlineData(-16, 1, "west-weald")]
    [InlineData(-27, 2, "west-weald")]
    [InlineData(-36, -4, "kvatch")]
    [InlineData(-32, -2, "west-weald")]
    [InlineData(-40, -4, "kvatch")]
    [InlineData(-42, -11, "gold-coast")]
    [InlineData(-48, -9, "gold-coast")]
    [InlineData(-47, -9, "gold-coast")]
    [InlineData(-46, -9, "gold-coast")]
    [InlineData(-48, -8, "gold-coast")]
    [InlineData(-47, -8, "gold-coast")]
    [InlineData(-46, -8, "gold-coast")]
    [InlineData(-48, -7, "gold-coast")]
    [InlineData(-47, -7, "gold-coast")]
    [InlineData(-46, -7, "gold-coast")]
    [InlineData(-46, -3, "gold-coast")]
    [InlineData(-48, 0, "gold-coast")]
    [InlineData(5, 5, "great-forest")]
    [InlineData(5, 0, "great-forest")]
    [InlineData(7, -1, "great-forest")]
    [InlineData(21, 5, "cheydinhal")]
    [InlineData(16, -8, "nibenay")]
    [InlineData(45, -5, "nibenay")]
    [InlineData(42, 3, "nibenay")]
    [InlineData(35, 5, "cheydinhal")]
    [InlineData(27, -5, "cheydinhal")]
    [InlineData(32, -6, "cheydinhal")]
    [InlineData(31, -22, "blackwood")]
    [InlineData(19, -28, "blackwood")]
    [InlineData(17, -28, "blackwood")]
    [InlineData(26, -30, "blackwood")]
    [InlineData(20, -35, "blackwood")]
    [InlineData(21, -35, "blackwood")]
    [InlineData(22, -35, "blackwood")]
    [InlineData(23, -35, "blackwood")]
    [InlineData(20, -34, "blackwood")]
    [InlineData(21, -34, "blackwood")]
    [InlineData(22, -34, "blackwood")]
    [InlineData(23, -34, "blackwood")]
    public void Every_landmark_lands_in_its_shipped_zone(int x, int y, string expected) =>
        Assert.Equal(expected, new ZoneMap(Shipped()).Find(Outside(x, y))?.Id);

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(5, 0, true)]
    [InlineData(10, 10, true)]
    [InlineData(5, 5, true)]
    [InlineData(-1, 5, false)]
    [InlineData(11, 5, false)]
    public void Polygon_edges_and_vertices_are_inclusive(int x, int y, bool expected) =>
        Assert.Equal(expected, ZoneMap.Contains([[0, 0], [10, 0], [10, 10], [0, 10]], x, y));

    [Fact]
    public void A_concave_polygon_does_not_fill_its_missing_corner()
    {
        int[][] shape = [[0, 0], [6, 0], [6, 2], [2, 2], [2, 6], [0, 6]];
        Assert.True(ZoneMap.Contains(shape, 1, 5));
        Assert.True(ZoneMap.Contains(shape.Reverse().ToArray(), 5, 1));
        Assert.False(ZoneMap.Contains(shape, 4, 4));
    }

    [Theory]
    [InlineData(4, 15, "imperial-city")]
    [InlineData(27, 22, "blue-road")]
    [InlineData(16, -8, "nibenay")]
    [InlineData(-36, -4, "kvatch")]
    [InlineData(-30, 0, "west-weald")]
    public void Specific_entries_win_where_the_game_regions_overlap(int x, int y, string expected)
    {
        var settings = Shipped();
        Assert.True(settings.Entries.Count(zone => zone.Shapes.Any(shape => ZoneMap.Contains(shape, x, y))) > 1);
        Assert.Equal(expected, new ZoneMap(settings).Find(Outside(x, y))?.Id);
    }

    [Theory]
    [InlineData(0, 15, "heartlands", "imperial-city-fallback")]
    [InlineData(-27, 20, "colovian-highlands", "great-forest-fallback")]
    [InlineData(-32, -2, "west-weald", "kvatch-fallback")]
    [InlineData(29, 31, "cheydinhal", "jerall-fallback")]
    public void Region_entries_win_over_the_old_rectangles(int x, int y, string expected, string fallback)
    {
        var settings = Shipped();
        var old = Assert.Single(settings.Entries, zone => zone.Id == fallback);
        Assert.Contains(old.Shapes, shape => ZoneMap.Contains(shape, x, y));
        Assert.Equal(expected, new ZoneMap(settings).Find(Outside(x, y))?.Id);
        var firstFallback = settings.Entries.FindIndex(zone => zone.Id.EndsWith("-fallback", StringComparison.Ordinal));
        Assert.All(settings.Entries.Skip(firstFallback), zone => Assert.EndsWith("-fallback", zone.Id));
    }

    [Theory]
    [InlineData(0, -15, "nibenay-fallback")]
    [InlineData(60, 60, "jerall-fallback")]
    [InlineData(60, -60, "blackwood-fallback")]
    public void Fallbacks_fill_gaps_outside_the_catalog_outlines(int x, int y, string expected) =>
        Assert.Equal(expected, new ZoneMap(Shipped()).Find(Outside(x, y))?.Id);

    [Fact]
    public void No_cell_of_the_old_shapes_loses_its_zone()
    {
        var settings = Shipped();
        var map = new ZoneMap(settings);
        foreach (var zone in settings.Entries.Where(zone => zone.Id.EndsWith("-fallback", StringComparison.Ordinal)))
        foreach (var shape in zone.Shapes)
        for (var x = shape.Min(point => point[0]); x <= shape.Max(point => point[0]); x++)
        for (var y = shape.Min(point => point[1]); y <= shape.Max(point => point[1]); y++)
        {
            if (ZoneMap.Contains(shape, x, y))
                Assert.NotNull(map.Find(Outside(x, y)));
        }
    }

    [Fact]
    public void Regenerated_polygons_keep_distinct_neighbors_and_both_translated_names()
    {
        var settings = Shipped();
        Assert.Equal(24, settings.Entries.Count);
        foreach (var zone in settings.Entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(zone.Named("en")));
            Assert.False(string.IsNullOrWhiteSpace(zone.Named("pl")));
            foreach (var shape in zone.Shapes)
            for (var index = 0; index < shape.Length; index++)
                Assert.False(shape[index].SequenceEqual(shape[(index + 1) % shape.Length]));
        }
    }

    [Fact]
    public void Oblivion_uses_all_eleven_catalog_worldspaces_and_no_Tamriel_shape()
    {
        var zone = Assert.Single(Shipped().Entries, zone => zone.Id == "oblivion");
        Assert.Equal(ZoneMode.PartyLowest, zone.Mode);
        Assert.Empty(zone.Shapes);
        Assert.Equal(Enumerable.Range(1, 7).Select(number => $"OblivionRD{number:000}").Concat(
            ["MS13CheydinhalOblivionWorld", "MQ10BrumaOblivionGate", "OblivionMQKvatch", "MQ14OblivionWorld"]), zone.Areas);
    }

    [Fact]
    public void Every_shipped_area_matches_its_zone_away_from_the_Tamriel_grid()
    {
        var settings = Shipped();
        var map = new ZoneMap(settings);
        foreach (var zone in settings.Entries)
        foreach (var area in zone.Areas)
            Assert.Equal(zone.Id, map.Find(new ZoneCell(OblivionAreaIds.BuildExterior(area), ParentCellKind.Exterior, 0, 0))?.Id);
    }

    [Fact]
    public void Interiors_unknown_cells_and_other_worlds_are_not_zoned()
    {
        var map = new ZoneMap(Shipped());
        Assert.Null(map.Find(Outside(65, 65)));
        Assert.Null(map.Find(Outside(27, 22) with { Kind = ParentCellKind.Interior }));
        Assert.Null(map.Find(Outside(27, 22) with { Kind = ParentCellKind.Unknown }));
        Assert.Null(map.Find(Outside(27, 22) with { Area = OblivionAreaIds.BuildExterior("L_PersistentDungeon-Test") }));
        Assert.Null(map.Find(Outside(27, 22) with { Area = OblivionAreaIds.BuildExterior("UnmappedWorld") }));
    }

    [Theory]
    [InlineData(0x96AC5u)]
    [InlineData(0x96228u)]
    [InlineData(0xABB3u)]
    public void Imperial_City_sewer_interiors_are_one_dungeon_for_levels_10_to_15(uint cell)
    {
        var settings = Shipped();
        var interior = new ZoneCell(OblivionAreaIds.BuildInterior(cell), ParentCellKind.Interior, 0, 0);
        var zone = new ZoneMap(settings).Find(interior);
        Assert.Equal("imperial-sewers", zone?.Id);
        Assert.Equal((10, 15), (zone!.MinLevel, zone.MaxLevel));
        Assert.InRange(ZoneLevels.ForCell(zone, interior, 40), 10, 15);
        // The interior's own grid values are ignored, only its form id picks the level.
        Assert.Equal(ZoneLevels.ForCell(zone, interior, 1), ZoneLevels.ForCell(zone, interior with { X = 7, Y = -3 }, 1));
    }

    [Fact]
    public void Sewer_sections_get_different_levels_and_unlisted_interiors_stay_unzoned()
    {
        var settings = Shipped();
        var map = new ZoneMap(settings);
        var sewers = Assert.Single(settings.Entries, zone => zone.Id == "imperial-sewers");
        Assert.Equal(21, sewers.Interiors.Count);
        var levels = sewers.Interiors.Select(cell => ZoneLevels.ForCell(sewers,
            new ZoneCell(OblivionAreaIds.BuildInterior(Convert.ToUInt32(cell, 16)), ParentCellKind.Interior, 0, 0), 1)).ToHashSet();
        Assert.Contains(10, levels);
        Assert.Contains(15, levels);
        Assert.Null(map.Find(new ZoneCell(OblivionAreaIds.BuildInterior(0x12345), ParentCellKind.Interior, 0, 0)));
        Assert.Equal(0x96AC5u, ZoneCell.InteriorFormId(OblivionAreaIds.BuildInterior(0x96AC5)));
        Assert.Equal(0u, ZoneCell.InteriorFormId(GameSpace.Outside));
    }

    [Theory]
    [InlineData("not-hex", (int)ZoneMode.Fixed)]
    [InlineData("0", (int)ZoneMode.Fixed)]
    [InlineData("00096AC5", (int)ZoneMode.Gradient)]
    public void Interiors_must_be_form_ids_and_cannot_use_a_gradient(string cell, int mode)
    {
        var settings = new ZoneSettings { Entries = [new ZoneDefinition
        {
            Id = "sewers", Name = new() { ["en"] = "Sewers", ["pl"] = "Kanały" }, Mode = (ZoneMode)mode, MinLevel = 10, MaxLevel = 15,
            Interiors = [cell],
        }] };
        Assert.Throws<InvalidOperationException>(settings.RequireValid);
    }

    [Fact]
    public void Disabling_zones_disables_both_shapes_and_named_areas()
    {
        var settings = Shipped();
        settings.Enabled = false;
        var map = new ZoneMap(settings);
        Assert.Null(map.Find(Outside(27, 22)));
        Assert.Null(map.Find(Outside(0, 0) with { Area = OblivionAreaIds.BuildExterior("AnvilWorld") }));
        Assert.Null(map.Find(new ZoneCell(OblivionAreaIds.BuildInterior(0x96AC5), ParentCellKind.Interior, 0, 0)));
    }

    [Fact]
    public void Fixed_levels_are_stable_within_the_range_and_ignore_players()
    {
        var zone = new ZoneDefinition { Mode = ZoneMode.Fixed, MinLevel = 15, MaxLevel = 25 };
        var found = new HashSet<int>();
        for (var x = -64; x <= 64; x++)
        {
            var level = ZoneLevels.ForCell(zone, Outside(x, 35), 1);
            Assert.InRange(level, zone.MinLevel, zone.MaxLevel);
            Assert.Equal(level, ZoneLevels.ForCell(zone, Outside(x, 35), 60));
            found.Add(level);
        }
        Assert.Contains(zone.MinLevel, found);
        Assert.Contains(zone.MaxLevel, found);
    }

    [Theory]
    [InlineData((int)ZoneMode.ScalesAbove, 3, 40)]
    [InlineData((int)ZoneMode.ScalesAbove, 60, 60)]
    [InlineData((int)ZoneMode.PartyLowest, 3, 3)]
    [InlineData((int)ZoneMode.PartyLowest, 60, 60)]
    [InlineData((int)ZoneMode.PartyLowest, 0, ZoneLevels.Minimum)]
    public void Scaling_modes_follow_the_lowest_and_only_ScalesAbove_has_a_floor(int mode, int lowest, int expected) =>
        Assert.Equal(expected, ZoneLevels.ForCell(new ZoneDefinition { Mode = (ZoneMode)mode, MinLevel = 40, MaxLevel = 40 }, Outside(0, 0), lowest));

    [Fact]
    public void A_party_expands_to_remote_online_members_and_invitations_do_not_count()
    {
        var leader = Player();
        var remote = Player();
        var invited = Player();
        var solo = Player();
        var book = new PartyBook();
        book.Join(book.Invite(leader, remote));
        book.Invite(leader, invited);
        var levels = new Dictionary<OnlinePlayer, int> { [leader] = 50, [remote] = 7, [invited] = 1, [solo] = 12 };
        IEnumerable<OnlinePlayer> Members(OnlinePlayer player) => book.Find(player)?.Members ?? [player];
        int? Level(OnlinePlayer player) => player.IsLive ? levels[player] : null;

        Assert.Equal(7, ZoneLevels.Lowest([leader, solo], Members, Level));
        Assert.Equal(7, ZoneLevels.Lowest([leader, remote], Members, Level));
        remote.IsLive = false;
        Assert.Equal(12, ZoneLevels.Lowest([leader, solo], Members, Level));
        Assert.Equal(50, ZoneLevels.Lowest([leader], Members, Level));
        Assert.Equal(ZoneLevels.Minimum, ZoneLevels.Lowest(Array.Empty<OnlinePlayer>(), Members, Level));
    }

    [Theory]
    [InlineData(1, 40, -39)]
    [InlineData(40, 1, 19)]
    [InlineData(40, 40, -20)]
    [InlineData(0, 10, -9)]
    [InlineData(-10, 0, 1)]
    [InlineData(int.MaxValue, 1, ushort.MaxValue - 1)]
    public void Offsets_reproduce_the_cell_level_even_with_zero_session_level(int level, int session, int expected) =>
        Assert.Equal(expected, ZoneLevels.Offset(level, session));

    [Theory]
    [InlineData(true, 20, 10, -18)]
    [InlineData(true, 100, 1, -30)]
    [InlineData(false, 20, 10, -5)]
    [InlineData(false, 100, 1, -3.5)]
    [InlineData(true, 10, 10, -10)]
    [InlineData(false, 10, 20, -10)]
    public void Damage_uses_the_gap_and_respects_both_caps(bool npcAttacks, int zoneLevel, int playerLevel, float expected) =>
        Assert.Equal(expected, ZoneLevels.Damage(-10, npcAttacks, zoneLevel, playerLevel, new ZoneSettings()), 4);

    [Fact]
    public void Healing_and_zero_damage_are_unchanged()
    {
        Assert.Equal(10, ZoneLevels.Damage(10, true, 40, 1, new ZoneSettings()));
        Assert.Equal(0, ZoneLevels.Damage(0, false, 40, 1, new ZoneSettings()));
    }

    [Fact]
    public void Invalid_config_is_refused_before_any_entity_is_changed()
    {
        Assert.Throws<InvalidOperationException>(() => new ZoneSettings { TakenPerLevel = float.NaN }.RequireValid());
        Assert.Throws<InvalidOperationException>(() => new ZoneSettings { MinDealt = -1 }.RequireValid());
        var settings = Shipped();
        settings.Entries[0].Shapes = [[[1, 2]]];
        Assert.Throws<InvalidOperationException>(settings.RequireValid);
        settings = Shipped();
        settings.Entries[0].Mode = (ZoneMode)int.MaxValue;
        Assert.Throws<InvalidOperationException>(settings.RequireValid);
    }

    private static OnlinePlayer Player() => new(default, Guid.NewGuid(), DateTimeOffset.UnixEpoch);
}
