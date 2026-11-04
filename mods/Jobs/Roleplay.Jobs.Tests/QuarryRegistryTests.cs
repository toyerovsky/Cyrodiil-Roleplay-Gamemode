using System.Numerics;
using Roleplay.Jobs.Server;
using static Roleplay.Jobs.Tests.PluginMapMarkersTests;

namespace Roleplay.Jobs.Tests;

public sealed class QuarryRegistryTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("jobs-tests-").FullName;
    private readonly JobsConfig _config = new();
    private readonly FelledTreeStoreTests.FailOnErrorLogger _logger = new();

    public QuarryRegistryTests()
    {
        _config.Mining.PluginFolders = ["Data"];
        Directory.CreateDirectory(Path.Combine(_folder, "Data"));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_quarry_marker_in_a_plugin_becomes_a_quarry_in_world_space_and_other_markers_do_not()
    {
        File.WriteAllBytes(Path.Combine(_folder, "Data", "RoleplayWorld.esp"), Plugin(["Oblivion.esm"],
            Group("WRLD", 0,
                Group(0x0000003C, 1,
                    Record("REFR", 0x01000801, 0, Sub("XMRK", []), Sub("FULL", ZString("Great Forest Quarry")), Sub("DATA", Floats(700f, 1400f, 70f, 0f, 0f, 0f))),
                    Record("REFR", 0x01000802, 0, Sub("XMRK", []), Sub("FULL", ZString("Aleswell")), Sub("DATA", Floats(0f, 0f, 0f, 0f, 0f, 0f))))),
            Group("WRLD", 0,
                Record("WRLD", 0x01000900, 0, Sub("EDID", ZString("ChorrolWorld"))),
                Group(0x01000900, 1,
                    Record("REFR", 0x01000901, 0, Sub("XMRK", []), Sub("FULL", ZString("Town Quarry")), Sub("DATA", Floats(9f, 9f, 9f, 0f, 0f, 0f)))))));

        var quarry = Assert.Single(Registry().Quarries);

        Assert.Equal("Great Forest Quarry", quarry.Name);
        Assert.True(Vector3.Distance(new Vector3(1000f, -2000f, 100f), quarry.Center) < 0.01f, $"{quarry.Center}"); // 100/70 cm a unit, Y flipped
        Assert.Equal(2000f, quarry.Radius);
        Assert.True(quarry.Holds(new Vector3(2500f, -2000f, 100f)));
        Assert.False(quarry.Holds(new Vector3(3001f, -2000f, 100f)));
        Assert.Empty(_logger.Warnings);
    }

    [Fact]
    public void Config_quarries_stand_beside_marker_ones_and_markers_can_be_switched_off()
    {
        File.WriteAllBytes(Path.Combine(_folder, "Data", "RoleplayWorld.esp"), Plugin(["Oblivion.esm"],
            Group("WRLD", 0, Group(0x0000003C, 1,
                Record("REFR", 1, 0, Sub("XMRK", []), Sub("FULL", ZString("Quarry")), Sub("DATA", Floats(0f, 0f, 0f, 0f, 0f, 0f)))))));
        _config.Mining.Quarries.Add(new QuarryConfig { Name = "Old Quarry", X = 5f, Y = 6f, Z = 7f, Radius = 500f });

        Assert.Equal(["Old Quarry", "Quarry"], Registry().Quarries.Select(quarry => quarry.Name));

        _config.Mining.QuarriesFromMarkers = false;
        Assert.Equal(new QuarryArea("Old Quarry", new Vector3(5f, 6f, 7f), 500f), Assert.Single(Registry().Quarries));
    }

    [Fact]
    public void A_plugin_a_mod_ships_as_client_content_counts_and_one_found_twice_is_read_once()
    {
        // As a server lays it out: mods/<mod>/client/<plugin>, and the same plugin in the modloader's game data
        _config.Mining.PluginFolders = ["mods", "Data"];
        var plugin = Plugin(["Oblivion.esm"],
            Group("WRLD", 0, Group(0x0000003C, 1,
                Record("REFR", 1, 0, Sub("XMRK", []), Sub("FULL", ZString("Aleswell Quarry")), Sub("DATA", Floats(3000f, 109456f, 11312f, 0f, 0f, 0f))))));
        Directory.CreateDirectory(Path.Combine(_folder, "mods", "Roleplay.Jobs", "client"));
        File.WriteAllBytes(Path.Combine(_folder, "mods", "Roleplay.Jobs", "client", "QuarryJob.esp"), plugin);
        File.WriteAllBytes(Path.Combine(_folder, "Data", "QuarryJob.esp"), plugin);

        var quarry = Assert.Single(Registry().Quarries);

        Assert.Equal("Aleswell Quarry", quarry.Name);
    }

    [Fact]
    public void A_broken_plugin_gives_no_quarries_and_a_warning()
    {
        File.WriteAllBytes(Path.Combine(_folder, "Data", "Broken.esp"), [.. "GRUP"u8, 255, 255, 0, 0]);
        File.WriteAllBytes(Path.Combine(_folder, "Data", "Padding.esp"), new byte[30]);

        Assert.Empty(Registry().Quarries);
        Assert.NotEmpty(_logger.Warnings);
    }

    private QuarryRegistry Registry()
    {
        var registry = new QuarryRegistry(_config, _logger);
        registry.UseFolder(_folder);
        return registry;
    }
}
