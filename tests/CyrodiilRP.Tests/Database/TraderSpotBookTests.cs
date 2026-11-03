using CyrodiilRP.Data;
using CyrodiilRP.Data.Traders;

namespace CyrodiilRP.Tests.Database;

public sealed class TraderSpotBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const long NpcRefId = 0xFF000123;

    [Fact]
    public async Task A_spot_is_kept_with_where_and_by_whom_and_learns_its_trader()
    {
        var id = await pg.RunAsync(db => TraderSpotBook.AddAsync(db, CreateSpot(), default));

        await pg.RunAsync(db => TraderSpotBook.SetNpcAsync(db, id, NpcRefId, default));

        var kept = Assert.Single(await pg.RunAsync(db => TraderSpotBook.LoadAllAsync(db, default)), spot => spot.Id == id);
        Assert.Equal("general", kept.Shop);
        Assert.Equal("L_Tamriel", kept.Area);
        Assert.Equal("3,24", kept.Cell);
        Assert.Equal(1250.5f, kept.X);
        Assert.Equal(-90f, kept.Yaw);
        Assert.Equal(NpcRefId, kept.NpcRefId);
        Assert.Equal("account:x", kept.PlacedByRef);
    }

    [Fact]
    public async Task A_spot_removed_is_gone_once()
    {
        var id = await pg.RunAsync(db => TraderSpotBook.AddAsync(db, CreateSpot(), default));

        Assert.True(await pg.RunAsync(db => TraderSpotBook.RemoveAsync(db, id, default)));
        Assert.False(await pg.RunAsync(db => TraderSpotBook.RemoveAsync(db, id, default)));
        Assert.DoesNotContain(await pg.RunAsync(db => TraderSpotBook.LoadAllAsync(db, default)), spot => spot.Id == id);
    }

    private static TraderSpot CreateSpot() => new()
    {
        Shop = "general",
        Area = "L_Tamriel",
        Cell = "3,24",
        X = 1250.5f,
        Y = 98000f,
        Z = 4600f,
        Yaw = -90f,
        PlacedByRef = "account:x",
        PlacedAt = Seeds.Now,
    };
}
