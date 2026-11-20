using CyrodiilRP.Data;
using CyrodiilRP.Data.Economy;
using CyrodiilRP.Data.Gambling;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class GamblingBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const int Stake = 40;

    [Fact]
    public async Task A_game_keeps_names_rounds_and_escrow_and_start_retries_are_idempotent()
    {
        var game = await GameAsync();
        var id = await pg.RunAsync(db => GamblingBook.StartAsync(db, game, default));
        var again = await pg.RunAsync(db => GamblingBook.StartAsync(db, game, default));
        Assert.Equal(id, again);
        game.Id = id;
        game.FirstRaises = 10;
        game.SecondRaises = 10;
        game.Pot += 20;
        game.Rounds = "[{\"firstHand\":[6,6,6,6,6]}]";
        Assert.True(await pg.RunAsync(db => GamblingBook.SaveAsync(db, game, default)));
        var recent = await pg.RunAsync(db => GamblingBook.RecentAsync(db, game.FirstCharacterId, default));
        var saved = Assert.Single(recent);
        Assert.Equal(game.Pot, saved.Pot);
        Assert.Equal(game.FirstName, saved.FirstName);
        Assert.Contains("firstHand", saved.Rounds);
    }

    [Fact]
    public async Task Settlement_and_the_single_winners_payment_commit_together_even_when_retried()
    {
        var game = await StartedAsync();
        game.Status = GamblingStatus.Settled;
        game.WinnerCharacterId = game.SecondCharacterId;
        game.Cut = 3;
        game.EndedAt = Seeds.Now;
        Assert.True(await pg.RunAsync(db => GamblingBook.FinishAsync(db, game, default)));
        Assert.False(await pg.RunAsync(db => GamblingBook.FinishAsync(db, game, default)));
        Assert.Empty(await pg.RunAsync(db => GoldDeliveryBook.LoadPendingAsync(db, game.FirstCharacterId, default)));
        var payment = Assert.Single(await pg.RunAsync(db => GoldDeliveryBook.LoadPendingAsync(db, game.SecondCharacterId, default)));
        Assert.Equal(2 * Stake - game.Cut, payment.Amount);
        Assert.True(await pg.RunAsync(db => GoldDeliveryBook.ClaimAsync(db, payment.Id, Seeds.Now, default)));
        Assert.False(await pg.RunAsync(db => GoldDeliveryBook.ClaimAsync(db, payment.Id, Seeds.Now, default)));
    }

    [Theory]
    [InlineData(GamblingStatus.Refunded)]
    [InlineData(GamblingStatus.Cancelled)]
    public async Task A_draw_or_cancellation_returns_exactly_each_sides_stake_and_raises(GamblingStatus status)
    {
        var game = await StartedAsync();
        game.FirstRaises = 17;
        game.SecondRaises = 7;
        game.Pot += 24;
        game.Status = status;
        game.EndedAt = Seeds.Now;
        await pg.RunAsync(db => GamblingBook.FinishAsync(db, game, default));
        Assert.Equal(Stake + 17, Assert.Single(await pg.RunAsync(db => GoldDeliveryBook.LoadPendingAsync(db, game.FirstCharacterId, default))).Amount);
        Assert.Equal(Stake + 7, Assert.Single(await pg.RunAsync(db => GoldDeliveryBook.LoadPendingAsync(db, game.SecondCharacterId, default))).Amount);
    }

    [Fact]
    public async Task Startup_refunds_only_open_games_once()
    {
        var open = await StartedAsync();
        var closed = await StartedAsync();
        closed.Status = GamblingStatus.Cancelled;
        closed.EndedAt = Seeds.Now;
        await pg.RunAsync(db => GamblingBook.FinishAsync(db, closed, default));
        var recovered = await pg.RunAsync(db => GamblingBook.RefundOpenAsync(db, Seeds.Now, default));
        Assert.Contains(recovered, game => game.Id == open.Id);
        Assert.DoesNotContain(recovered, game => game.Id == closed.Id);
        Assert.Empty(await pg.RunAsync(db => GamblingBook.RefundOpenAsync(db, Seeds.Now, default)));
        Assert.Equal(Stake, Assert.Single(await pg.RunAsync(db => GoldDeliveryBook.LoadPendingAsync(db, open.FirstCharacterId, default))).Amount);
    }

    [Fact]
    public async Task Removing_a_table_keeps_its_game_history()
    {
        var game = await StartedAsync();
        Assert.True(await pg.RunAsync(db => GamblingBook.RemoveAsync(db, game.TableId, Seeds.Now, default)));
        Assert.DoesNotContain(await pg.RunAsync(db => GamblingBook.LoadTablesAsync(db, default)), table => table.Id == game.TableId);
        Assert.Single(await pg.RunAsync(db => GamblingBook.RecentAsync(db, game.SecondCharacterId, default)));
    }

    [Fact]
    public async Task The_admin_log_filters_both_historical_names_and_escapes_like_wildcards()
    {
        var game = await StartedAsync();
        var page = await pg.RunAsync(db => GamblingBook.PageAsync(db, game.SecondName.ToLowerInvariant(), null, default));
        Assert.Contains(page.Games, row => row.Id == game.Id);
        Assert.Empty((await pg.RunAsync(db => GamblingBook.PageAsync(db, "%", null, default))).Games);
        Assert.DoesNotContain((await pg.RunAsync(db => GamblingBook.PageAsync(db, game.SecondName, game.Id, default))).Games, row => row.Id == game.Id);
    }

    [Fact]
    public async Task Invalid_escrow_is_refused_by_the_database()
    {
        var game = await GameAsync();
        game.Pot = 0;
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => pg.RunAsync(db => GamblingBook.StartAsync(db, game, default)));
        Assert.Equal("ck_gambling_games_gold", Assert.IsType<Npgsql.PostgresException>(exception.InnerException).ConstraintName);
    }

    [Fact]
    public async Task History_resolves_offline_names_only_after_an_introduction_or_public_name()
    {
        var game = await StartedAsync();
        var stranger = await pg.RunAsync(db => GamblingBook.HistoryAsync(db, game.FirstCharacterId, default));
        Assert.DoesNotContain(game.SecondCharacterId, stranger.KnownNames);
        await pg.RunAsync(async db =>
        {
            db.LoreIntroductions.Add(new LoreIntroduction { KnowerCharacterId = game.FirstCharacterId, KnownCharacterId = game.SecondCharacterId, At = Seeds.Now });
            await db.SaveChangesAsync();
        });
        Assert.Contains(game.SecondCharacterId, (await pg.RunAsync(db => GamblingBook.HistoryAsync(db, game.FirstCharacterId, default))).KnownNames);
        await pg.RunAsync(db => db.Characters.Where(character => character.Id == game.FirstCharacterId)
            .ExecuteUpdateAsync(set => set.SetProperty(character => character.NamePublic, true)));
        Assert.Contains(game.FirstCharacterId, (await pg.RunAsync(db => GamblingBook.HistoryAsync(db, game.SecondCharacterId, default))).KnownNames);
    }

    private async Task<GamblingGame> StartedAsync()
    {
        var game = await GameAsync();
        game.Id = await pg.RunAsync(db => GamblingBook.StartAsync(db, game, default));
        return game;
    }

    private async Task<GamblingGame> GameAsync()
    {
        var first = await Seeds.CreateCharacterAsync(pg.Factory);
        var second = await Seeds.CreateCharacterAsync(pg.Factory);
        var table = new GamblingTable { Area = "L_PersistentDungeon-Inn", Cell = "Inn", PlacedByRef = "server", PlacedAt = Seeds.Now };
        var id = await pg.RunAsync(db => GamblingBook.PlaceAsync(db, table, default));
        return new GamblingGame { OperationId = Guid.NewGuid(), TableId = id, Mode = GamblingMode.Poker, FirstCharacterId = first.Id,
            SecondCharacterId = second.Id, FirstName = "Marcus " + Guid.NewGuid().ToString("N"), SecondName = "Lucia " + Guid.NewGuid().ToString("N"),
            Stake = Stake, Pot = 2 * Stake, Status = GamblingStatus.Open, Rounds = "[]", StartedAt = Seeds.Now, UpdatedAt = Seeds.Now };
    }
}
