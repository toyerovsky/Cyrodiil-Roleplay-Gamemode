using CyrodiilRP.Data;
using CyrodiilRP.Data.Spawn;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class SpawnPointBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task There_is_no_spawn_until_set_and_setting_again_moves_the_one_there_is()
    {
        Assert.Null(await pg.RunAsync(db => SpawnPointBook.GetAsync(db, default)));

        await pg.RunAsync(db => SpawnPointBook.SetAsync(db, CreatePoint(1250.5f, -90f, "account:a"), default));
        await pg.RunAsync(db => SpawnPointBook.SetAsync(db, CreatePoint(-4000f, 45f, "account:b"), default));

        var kept = await pg.RunAsync(db => SpawnPointBook.GetAsync(db, default));
        Assert.NotNull(kept);
        Assert.Equal(-4000f, kept.X);
        Assert.Equal(45f, kept.Yaw);
        Assert.Equal("L_Tamriel", kept.Area);
        Assert.Equal("account:b", kept.SetByRef);
        Assert.Equal(1, await pg.RunAsync(db => db.SpawnPoints.CountAsync()));
    }

    private static SpawnPoint CreatePoint(float x, float yaw, string byRef) => new()
    {
        Area = "L_Tamriel",
        X = x,
        Y = 98000f,
        Z = 4600f,
        Yaw = yaw,
        SetByRef = byRef,
        SetAt = Seeds.Now,
    };
}
