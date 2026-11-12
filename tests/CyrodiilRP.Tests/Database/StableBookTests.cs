using CyrodiilRP.Data;
using CyrodiilRP.Data.Stable;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class StableBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_character_who_never_bought_a_horse_has_none()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Null(await FindAsync(character.Id));
    }

    [Fact]
    public async Task A_purchase_gives_the_character_the_horse_with_its_price_and_time()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await BuyAsync(character.Id, HorseKind.Bay, 1000);

        var horse = Assert.IsType<CharacterHorse>(await FindAsync(character.Id));
        Assert.Equal(HorseKind.Bay, horse.Horse);
        Assert.Equal(1000, horse.Price);
        Assert.Equal(Now, horse.BoughtAt);
    }

    [Fact]
    public async Task Another_purchase_replaces_the_horse_and_a_character_keeps_one()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await BuyAsync(character.Id, HorseKind.Paint, 500);

        await BuyAsync(character.Id, HorseKind.Black, 5000);

        Assert.Equal(HorseKind.Black, (await FindAsync(character.Id))!.Horse);
        Assert.Equal(1, await pg.RunAsync(db => db.CharacterHorses.CountAsync(h => h.CharacterId == character.Id)));
    }

    [Fact]
    public async Task One_characters_horse_is_not_anothers()
    {
        var owner = await Seeds.CreateCharacterAsync(pg.Factory);
        var other = await Seeds.CreateCharacterAsync(pg.Factory);

        await BuyAsync(owner.Id, HorseKind.White, 4000);

        Assert.Null(await FindAsync(other.Id));
    }

    [Fact]
    public async Task A_horse_is_stored_by_its_name()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await BuyAsync(character.Id, HorseKind.Chestnut, 2500);

        var stored = await pg.RunAsync(db => db.Database
            .SqlQuery<string>($"SELECT horse AS \"Value\" FROM character_horses WHERE character_id = {character.Id}")
            .SingleAsync());
        Assert.Equal(nameof(HorseKind.Chestnut), stored);
    }

    [Fact]
    public async Task A_horse_given_away_costs_nothing_and_none_costs_less()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await BuyAsync(character.Id, HorseKind.Paint, 0);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BuyAsync(character.Id, HorseKind.Bay, -1));

        Assert.Equal(0, (await FindAsync(character.Id))!.Price);
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE character_horses SET price = -1 WHERE character_id = {character.Id}")));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal("ck_character_horses_price", refusal.ConstraintName);
    }

    [Fact]
    public async Task A_deleted_character_takes_its_horse_along()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await BuyAsync(character.Id, HorseKind.Bay, 1000);

        await pg.RunAsync(db => db.Characters.Where(c => c.Id == character.Id).ExecuteDeleteAsync());

        Assert.False(await pg.RunAsync(db => db.CharacterHorses.AnyAsync(h => h.CharacterId == character.Id)));
    }

    private Task<CharacterHorse?> FindAsync(long characterId) => pg.RunAsync(db => StableBook.FindAsync(db, characterId, default));

    private Task BuyAsync(long characterId, HorseKind horse, int price) =>
        pg.RunAsync(db => StableBook.BuyAsync(db, characterId, horse, price, Now, default));
}
