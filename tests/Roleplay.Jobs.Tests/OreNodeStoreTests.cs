using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class OreNodeStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("jobs-tests-").FullName;
    private readonly JobsConfig _config = new();
    private readonly FelledTreeStoreTests.FailOnErrorLogger _logger = new();

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Placed_and_mined_nodes_survive_a_restart()
    {
        var iron = new OreNode { Kind = "Iron", X = 100f, Y = -200f, Z = 30f, Yaw = 90f, Scale = 1.5f };
        var rich = new OreNode { Kind = "Rich", X = -5f, Y = 7f, Z = 0f };
        var growsBack = new DateTime(2026, 11, 3, 12, 30, 0, DateTimeKind.Utc);

        var first = Store();
        first.AddPlaced(iron);
        first.AddPlaced(rich);
        first.SetMined(OreYield.NodeId(iron), growsBack);
        first.SetMined("jobs/ore/Iron|1|2|3", growsBack.AddMinutes(1));
        first.ClearMined("jobs/ore/Iron|1|2|3");

        var second = Store();
        Assert.Equal([OreYield.NodeId(iron), OreYield.NodeId(rich)], second.Placed.Select(OreYield.NodeId));
        Assert.Equal(1.5f, second.Placed[0].Scale);
        Assert.Equal(90f, second.Placed[0].Yaw);
        Assert.Equal(growsBack, second.Mined.Single(pair => pair.Key == OreYield.NodeId(iron)).Value);
        Assert.Single(second.Mined);
    }

    [Fact]
    public void Removing_a_placed_node_forgets_that_it_was_mined()
    {
        var iron = new OreNode { Kind = "Iron", X = 100f, Y = -200f, Z = 30f };
        var store = Store();
        store.AddPlaced(iron);
        store.SetMined(OreYield.NodeId(iron), DateTime.UtcNow);

        Assert.True(store.RemovePlaced(OreYield.NodeId(iron)));
        Assert.False(Store().RemovePlaced(OreYield.NodeId(iron)));

        Assert.Empty(Store().Placed);
        Assert.Empty(Store().Mined);
    }

    [Fact]
    public void An_unreadable_file_is_kept_aside_with_nothing_placed_or_mined()
    {
        var path = Path.Combine(_folder, _config.NodeSaveFile);
        File.WriteAllText(path, "{ not json");

        var store = Store();

        Assert.Empty(store.Placed);
        Assert.Empty(store.Mined);
        Assert.Equal("{ not json", File.ReadAllText(path + ".broken"));
        Assert.Single(_logger.Warnings);
    }

    private OreNodeStore Store()
    {
        var store = new OreNodeStore(_config, _logger);
        store.UseFolder(_folder);
        return store;
    }
}
