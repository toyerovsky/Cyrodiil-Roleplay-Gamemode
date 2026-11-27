using CyrodiilRP.Data;
using CyrodiilRP.Data.Building;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class PlotBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_plot_and_its_pieces_are_read_back_as_they_were_written()
    {
        var plot = await AddPlotAsync();
        var piece = await AddPieceAsync(plot, "furniture.table");

        var read = Assert.Single(await pg.RunAsync(db => PlotBook.LoadPlotsAsync(db, default)), p => p.Id == plot);
        Assert.Equal([3, -7, 12, 8], [read.FirstTileX, read.FirstTileY, read.Width, read.Depth]);
        Assert.Equal([105.5f, 1500f], [read.GroundZ, read.Height]);
        Assert.Equal(PlotKind.Industrial, read.Kind);
        Assert.Equal([2500, 40], [read.Price, read.WeeklyTax]);
        Assert.Null(read.OwnerCharacterId);
        var placed = Assert.Single(await pg.RunAsync(db => PlotBook.LoadObjectsAsync(db, default)), o => o.Id == piece);
        Assert.Equal(plot, placed.PlotId);
        Assert.Equal("furniture.table", placed.Piece);
        Assert.Equal(PlotObjectState.Built, placed.State);
        Assert.Equal([10f, 20f, 30f, 90f], [placed.X, placed.Y, placed.Z, placed.Yaw]);
    }

    [Fact]
    public async Task A_piece_that_is_the_game_s_own_object_keeps_its_ref_id_and_a_mesh_none()
    {
        var plot = await AddPlotAsync();
        var chair = await pg.RunAsync(db => PlotBook.AddObjectAsync(db, new PlotObject
        {
            PlotId = plot,
            Piece = "furniture.chair",
            State = PlotObjectState.Built,
            RefId = 0xFF000123,
            PlacedByRef = "account:test",
            PlacedAt = Now,
        }, default));
        var mesh = await AddPieceAsync(plot, "furniture.table");

        var read = await pg.RunAsync(db => PlotBook.LoadObjectsAsync(db, default));
        Assert.Equal(0xFF000123, read.Single(o => o.Id == chair).RefId);
        Assert.Null(read.Single(o => o.Id == mesh).RefId);
    }

    [Fact]
    public async Task A_piece_moved_keeps_its_id_and_a_gone_one_says_so()
    {
        var plot = await AddPlotAsync();
        var piece = await AddPieceAsync(plot, "fence.post");

        Assert.True(await pg.RunAsync(db => PlotBook.MoveObjectAsync(db, piece, 1f, 2f, 3f, 45f, default)));
        Assert.True(await pg.RunAsync(db => PlotBook.RemoveObjectAsync(db, piece, default)));
        Assert.False(await pg.RunAsync(db => PlotBook.MoveObjectAsync(db, piece, 1f, 2f, 3f, 45f, default)));
        Assert.False(await pg.RunAsync(db => PlotBook.RemoveObjectAsync(db, piece, default)));
    }

    [Fact]
    public async Task A_sign_moved_is_read_back_where_it_was_moved_and_a_gone_plot_says_so()
    {
        var plot = await AddPlotAsync();

        Assert.True(await pg.RunAsync(db => PlotBook.MoveSignAsync(db, plot, 10f, 20f, 30f, 90f, default)));
        var moved = Assert.Single(await pg.RunAsync(db => PlotBook.LoadPlotsAsync(db, default)), p => p.Id == plot);
        Assert.Equal([10f, 20f, 30f, 90f], [moved.SignX, moved.SignY, moved.SignZ, moved.SignYaw]);

        Assert.True(await pg.RunAsync(db => PlotBook.RemovePlotAsync(db, plot, default)));
        Assert.False(await pg.RunAsync(db => PlotBook.MoveSignAsync(db, plot, 10f, 20f, 30f, 90f, default)));
    }

    [Fact]
    public async Task A_plot_is_bought_once_and_neither_a_second_buyer_nor_a_gone_plot_gets_it()
    {
        var first = await Seeds.CreateCharacterAsync(pg.Factory);
        var second = await Seeds.CreateCharacterAsync(pg.Factory);
        var plot = await AddPlotAsync();

        Assert.True(await pg.RunAsync(db => PlotBook.BuyAsync(db, plot, first.Id, default)));
        Assert.False(await pg.RunAsync(db => PlotBook.BuyAsync(db, plot, second.Id, default)));
        Assert.Equal(first.Id, await pg.RunAsync(db => db.Plots.Where(p => p.Id == plot).Select(p => p.OwnerCharacterId).SingleAsync()));

        Assert.True(await pg.RunAsync(db => PlotBook.RemovePlotAsync(db, plot, default)));
        Assert.False(await pg.RunAsync(db => PlotBook.BuyAsync(db, plot, second.Id, default)));
    }

    [Fact]
    public async Task A_moved_piece_is_read_back_where_it_was_moved()
    {
        var plot = await AddPlotAsync();
        var piece = await AddPieceAsync(plot, "fence.post");

        await pg.RunAsync(db => PlotBook.MoveObjectAsync(db, piece, 1f, 2f, 3f, 45f, default));

        var moved = Assert.Single(await pg.RunAsync(db => PlotBook.LoadObjectsAsync(db, default)), o => o.Id == piece);
        Assert.Equal([1f, 2f, 3f, 45f], [moved.X, moved.Y, moved.Z, moved.Yaw]);
    }

    [Fact]
    public async Task A_plot_taken_away_takes_its_pieces_along_and_a_second_removal_says_it_was_gone()
    {
        var plot = await AddPlotAsync();
        var piece = await AddPieceAsync(plot, "house.tent-1");

        Assert.True(await pg.RunAsync(db => PlotBook.RemovePlotAsync(db, plot, default)));
        Assert.False(await pg.RunAsync(db => PlotBook.RemovePlotAsync(db, plot, default)));

        Assert.False(await pg.RunAsync(db => db.PlotObjects.AnyAsync(o => o.Id == piece)));
    }

    [Fact]
    public async Task A_plot_whose_owner_is_deleted_is_for_sale_again()
    {
        var owner = await Seeds.CreateCharacterAsync(pg.Factory);
        var plot = await AddPlotAsync();
        await pg.RunAsync(db => db.Plots.Where(p => p.Id == plot).ExecuteUpdateAsync(set => set.SetProperty(p => p.OwnerCharacterId, owner.Id)));

        await pg.RunAsync(db => db.Characters.Where(c => c.Id == owner.Id).ExecuteDeleteAsync());

        Assert.Null(await pg.RunAsync(db => db.Plots.Where(p => p.Id == plot).Select(p => p.OwnerCharacterId).SingleAsync()));
    }

    [Fact]
    public async Task A_piece_taken_apart_gives_way_to_its_parts_and_a_gone_one_writes_nothing()
    {
        var plot = await AddPlotAsync();
        var house = await AddPieceAsync(plot, "house.lower-1");

        Assert.True(await pg.RunAsync(db => PlotBook.ReplaceObjectAsync(db, house, [Part(plot, "kit.wall_a", 5f), Part(plot, "kit.roof_a", 15f)], default)));
        var read = await pg.RunAsync(db => PlotBook.LoadObjectsAsync(db, default));
        Assert.DoesNotContain(read, o => o.Id == house);
        var roof = Assert.Single(read, o => o.PlotId == plot && o.Piece == "kit.roof_a");
        Assert.Equal([15f, 2f], [roof.Pitch, roof.Roll]);

        Assert.False(await pg.RunAsync(db => PlotBook.ReplaceObjectAsync(db, house, [Part(plot, "kit.door_a", 0f)], default)));
        Assert.DoesNotContain(await pg.RunAsync(db => PlotBook.LoadObjectsAsync(db, default)), o => o.Piece == "kit.door_a");
    }

    private static PlotObject Part(long plot, string piece, float pitch) => new()
    {
        PlotId = plot,
        Piece = piece,
        Pitch = pitch,
        Yaw = 90f,
        Roll = 2f,
        State = PlotObjectState.Built,
        PlacedByRef = "account:test",
        PlacedAt = Now,
    };

    [Fact]
    public async Task A_plot_has_a_size_and_gold_of_0_or_more_and_a_piece_has_a_key()
    {
        var plot = await AddPlotAsync();

        await AssertRefusedByAsync("ck_plots_size", db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE plots SET width = 0 WHERE id = {plot}"));
        await AssertRefusedByAsync("ck_plots_size", db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE plots SET height = 0 WHERE id = {plot}"));
        await AssertRefusedByAsync("ck_plots_gold", db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE plots SET price = -1 WHERE id = {plot}"));
        await AssertRefusedByAsync("ck_plots_gold", db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE plots SET weekly_tax = -1 WHERE id = {plot}"));
        await AssertRefusedByAsync("ck_plot_objects_piece", db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO plot_objects (plot_id, piece, x, y, z, pitch, yaw, roll, state, placed_by_ref, placed_at)
            VALUES ({plot}, {""}, 0, 0, 0, 0, 0, 0, {"Built"}, {"account:test"}, {Now})
            """));
    }

    private Task<long> AddPlotAsync() => pg.RunAsync(db => PlotBook.AddPlotAsync(db, new Plot
    {
        Area = "L_Tamriel",
        FirstTileX = 3,
        FirstTileY = -7,
        Width = 12,
        Depth = 8,
        GroundZ = 105.5f,
        Height = 1500f,
        Kind = PlotKind.Industrial,
        Price = 2500,
        WeeklyTax = 40,
        SignX = 548f,
        SignY = -1280f,
        SignZ = 105.5f,
        SignYaw = 225f,
        MadeByRef = "account:test",
        MadeAt = Now,
    }, default));

    private Task<long> AddPieceAsync(long plot, string piece) => pg.RunAsync(db => PlotBook.AddObjectAsync(db, new PlotObject
    {
        PlotId = plot,
        Piece = piece,
        X = 10f,
        Y = 20f,
        Z = 30f,
        Yaw = 90f,
        State = PlotObjectState.Built,
        PlacedByRef = "account:test",
        PlacedAt = Now,
    }, default));

    // A raw write throws the database's refusal, a save through EF wraps it.
    private async Task AssertRefusedByAsync(string constraint, Func<CyrodiilContext, Task> write)
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(write));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal(constraint, refusal.ConstraintName);
    }
}
