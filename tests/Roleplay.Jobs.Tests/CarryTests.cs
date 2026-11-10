using System.Numerics;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class CarryTests : IDisposable
{
    private const float Reach = 150f;

    private readonly string _folder = Directory.CreateTempSubdirectory("jobs-tests-").FullName;
    private readonly JobsConfig _config = new();
    private readonly FelledTreeStoreTests.FailOnErrorLogger _logger = new();

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void The_one_in_front_is_picked_up_over_one_as_near_behind()
    {
        var behind = new Carryable("behind", "Barrel", -60f, 0f, 0f, 0f);
        var ahead = new Carryable("ahead", "Barrel", 60f, 0f, 0f, 0f);

        Assert.Equal("ahead", CarryReach.Nearest([behind, ahead], Vector3.Zero, yawDegrees: 0f, Reach)?.Id);
        Assert.Equal("behind", CarryReach.Nearest([behind, ahead], Vector3.Zero, yawDegrees: 180f, Reach)?.Id);
    }

    [Fact]
    public void Nothing_out_of_reach_or_on_another_floor_is_picked_up()
    {
        var far = new Carryable("far", "Crate", 400f, 0f, 0f, 0f);
        var upstairs = new Carryable("upstairs", "Crate", 60f, 0f, 300f, 0f);

        Assert.Null(CarryReach.Nearest([far, upstairs], Vector3.Zero, yawDegrees: 0f, Reach));
    }

    [Theory]
    [InlineData(100f, 0f, -90f, true)]   // in front, on the floor below the character's position
    [InlineData(100f, 0f, 60f, true)]    // on top of a stack
    [InlineData(300f, 0f, -90f, false)]  // too far across
    [InlineData(100f, 0f, -600f, false)] // down a cliff
    public void A_spot_to_put_down_is_near_the_player(float x, float y, float z, bool near)
    {
        Assert.Equal(near, CarryReach.IsWithin(Vector3.Zero, new Vector3(x, y, z), horizontal: 230f, vertical: 440f));
    }

    [Fact]
    public void Ahead_follows_the_facing()
    {
        var at = CarryReach.Ahead(new Vector3(10f, 20f, 5f), 90f, 100f);

        Assert.Equal(10f, at.X, 3);
        Assert.Equal(120f, at.Y, 3);
        Assert.Equal(5f, at.Z);
    }

    [Fact]
    public void Carryables_put_down_survive_a_restart_and_picked_up_ones_do_not()
    {
        var barrel = new Carryable("jobs/carry/a", "Barrel", 1f, 2f, 3f, 45f);
        var log = new Carryable("jobs/carry/b", "Log", -1f, -2f, -3f, 0f);
        var first = Store();
        first.Add(barrel);
        first.Add(log);
        Assert.True(first.Remove(log.Id));

        var second = Store();

        Assert.Equal([barrel], second.Lying);
        Assert.False(second.Remove(log.Id));
    }

    [Fact]
    public void An_unreadable_file_is_kept_aside_with_nothing_lying()
    {
        var path = Path.Combine(_folder, _config.Carrying.SaveFile);
        File.WriteAllText(path, "[ not json");

        Assert.Empty(Store().Lying);
        Assert.Equal("[ not json", File.ReadAllText(path + ".broken"));
        Assert.Single(_logger.Warnings);
    }

    private CarryableStore Store()
    {
        var store = new CarryableStore(_config, _logger);
        store.UseFolder(_folder);
        return store;
    }
}
