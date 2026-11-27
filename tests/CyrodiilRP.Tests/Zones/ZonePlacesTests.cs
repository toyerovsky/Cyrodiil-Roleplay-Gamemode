using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Zones;
using CyrodiilRP.Tests.Fakes;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Tests.Zones;

public sealed class ZonePlacesTests
{
    private const int InventoryMarkers = 351;
    private const int CandidatesPerGroup = 3;
    private const int SewerInteriors = 21;

    [Fact]
    public void Every_documented_entrance_matches_the_servers_zone_and_cell_level()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var config = JsonSerializer.Deserialize<RpConfig>(File.ReadAllText(Path.Combine(Repository.Root,
            "src", "CyrodiilRP.Server", "config.json")), options)!;
        var map = new ZoneMap(config.Zones);
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var currentZone = string.Empty;
        var solo = 0;
        var group = 0;
        var interiors = 0;
        foreach (var line in File.ReadLines(Path.Combine(Repository.Root, "docs", "features", "zone-places.md")))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal) && line.Contains(": ", StringComparison.Ordinal))
                currentZone = line[3..line.IndexOf(':')];
            if (!line.StartsWith("| ", StringComparison.Ordinal) || !line.Contains("(`", StringComparison.Ordinal))
                continue;

            var columns = line.Split('|', StringSplitOptions.TrimEntries);
            if (columns[2].StartsWith('`'))
            {
                // Interior cell rows: name, form id, level from the hashed form id.
                var formId = Convert.ToUInt32(columns[2].Trim('`'), 16);
                var interior = new ZoneCell(ReadyM.Relay.Common.Oblivion.OblivionAreaIds.BuildInterior(formId), ParentCellKind.Interior, 0, 0);
                var owner = Assert.IsType<ZoneDefinition>(map.Find(interior));
                Assert.Equal(currentZone, owner.Id);
                Assert.Equal(ZoneLevels.ForCell(owner, interior, ZoneLevels.Minimum).ToString(CultureInfo.InvariantCulture), columns[3]);
                interiors++;
                continue;
            }
            Assert.True(identifiers.Add(columns[1][(columns[1].IndexOf("(`", StringComparison.Ordinal) + 2)..^2]));
            var coordinates = columns[3].Split(',').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            var cell = new ZoneCell(GameSpace.Outside, ParentCellKind.Exterior, coordinates[0], coordinates[1]);
            var zone = Assert.IsType<ZoneDefinition>(map.Find(cell));
            Assert.Equal(currentZone, zone.Id);
            var level = zone.Mode switch
            {
                ZoneMode.Fixed or ZoneMode.Gradient => ZoneLevels.ForCell(zone, cell, ZoneLevels.Minimum).ToString(CultureInfo.InvariantCulture),
                ZoneMode.ScalesAbove or ZoneMode.PartyLowest => "party",
            };
            Assert.Equal(level, columns[4]);
            if (columns[5].Length > 0)
            {
                Assert.Contains(zone.Id, new[] { "imperial-city", "blue-road", "cheydinhal", "heartlands" });
                Assert.Contains(columns[2], new[] { "cave", "mine", "fort ruin", "elven ruin" });
                if (columns[5] == "Solo / solo")
                    solo++;
                else
                {
                    Assert.Equal("2-3 players / graczy", columns[5]);
                    group++;
                }
            }
        }
        Assert.Equal(InventoryMarkers, identifiers.Count);
        Assert.Equal(CandidatesPerGroup, solo);
        Assert.Equal(CandidatesPerGroup, group);
        Assert.Equal(SewerInteriors, interiors);
    }
}
