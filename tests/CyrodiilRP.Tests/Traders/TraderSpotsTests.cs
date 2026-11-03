using System.Numerics;
using CyrodiilRP.Data.Traders;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Traders;
using CyrodiilRP.Tests.Database;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Traders;

public sealed class TraderSpotsTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Shop = "general";
    private const string Area = "L_Tamriel";
    private const string Cell = "3,24";
    private const string PlacedBy = "account:test";

    private static readonly TimeSpan Drain = TimeSpan.FromSeconds(5);
    private static readonly Vector3 Position = new(1250f, -300f, 40f);

    private Persistence _persistence = null!;
    private TraderSpots _spots = null!;

    public Task InitializeAsync()
    {
        var log = new ListLogger();
        var clock = new SystemClock();
        var watchdog = new Watchdog(clock, log);
        var gameThread = new GameThread(watchdog);
        _persistence = new Persistence(new RpConfig { Database = { ConnectionString = pg.ConnectionString } }, log);
        _spots = new TraderSpots(_persistence, new Fire(gameThread, log), new Timers(gameThread, watchdog, clock), log);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _persistence.Writer.DisposeAsync();

    [Fact]
    public async Task A_removed_spot_leaves_memory_and_the_database_and_a_second_removal_says_it_was_gone()
    {
        var spot = await _spots.AddAsync(Shop, Area, Cell, Position, 0f, PlacedBy);

        Assert.True(await _spots.RemoveAsync(spot));
        Assert.False(await _spots.RemoveAsync(spot));

        Assert.DoesNotContain(spot, _spots.All);
        Assert.DoesNotContain(await pg.RunAsync(db => TraderSpotBook.LoadAllAsync(db, default)), row => row.Id == spot.Id);
    }

    // Taken from memory first, it would be gone until a restart read it back from the database.
    [Fact]
    public async Task A_spot_whose_removal_was_not_written_stays_as_the_database_keeps_it()
    {
        var spot = await _spots.AddAsync(Shop, Area, Cell, Position, 0f, PlacedBy);
        await _persistence.Writer.DrainAsync(Drain);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _spots.RemoveAsync(spot));

        Assert.Contains(spot, _spots.All);
        Assert.Contains(await pg.RunAsync(db => TraderSpotBook.LoadAllAsync(db, default)), row => row.Id == spot.Id);
    }
}
