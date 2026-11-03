using CyrodiilRP.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class ConstraintTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_ban_has_a_reason_and_a_reason_has_a_ban()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        await AssertRefusedByAsync("ck_accounts_ban", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE accounts SET banned_until = {Now} WHERE id = {account.Id}"));
        await AssertRefusedByAsync("ck_accounts_ban", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE accounts SET ban_reason = {"Spam"} WHERE id = {account.Id}"));
    }

    [Fact]
    public async Task One_account_per_readym_id()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        await AssertRefusedByAsync("ix_accounts_readym_id", db =>
        {
            db.Accounts.Add(new Account { ReadyMId = account.ReadyMId, CreatedAt = Now, LastSeenAt = Now });
            return db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Two_characters_never_share_a_name()
    {
        var taken = await Seeds.CreateCharacterAsync(pg.Factory);
        var other = await Seeds.CreateAccountAsync(pg.Factory);

        await AssertRefusedByAsync("ix_characters_name_key", db =>
        {
            db.Characters.Add(new Character
            {
                AccountId = other.Id,
                FirstName = taken.FirstName,
                LastName = taken.LastName,
                NameKey = Character.MakeNameKey(taken.FirstName, taken.LastName),
                CreatedAt = Now,
                LastPlayedAt = Now,
            });
            return db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Time_played_is_never_negative()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await AssertRefusedByAsync("ck_characters_played", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE characters SET played_seconds = -1 WHERE id = {character.Id}"));
    }

    [Fact]
    public async Task Owed_gold_is_more_than_nothing()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await AssertRefusedByAsync("ck_gold_deliveries_amount", db =>
        {
            db.GoldDeliveries.Add(new GoldDelivery { CharacterId = character.Id, Amount = 0, Reason = GoldDeliveryReason.StartingGold, CreatedAt = Now });
            return db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task An_account_with_a_character_is_never_deleted()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        await Seeds.CreateCharacterAsync(pg.Factory, account.Id);

        await AssertRefusedByAsync("fk_characters_accounts_account_id", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM accounts WHERE id = {account.Id}"));
    }

    [Fact]
    public async Task A_deleted_character_takes_its_conditions_and_owed_gold_along()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await pg.RunAsync(db =>
        {
            db.CharacterConditions.Add(new CharacterCondition { CharacterId = character.Id, Key = "admin.mute", AppliedAt = Now });
            db.GoldDeliveries.Add(new GoldDelivery { CharacterId = character.Id, Amount = 5, Reason = GoldDeliveryReason.StartingGold, CreatedAt = Now });
            return db.SaveChangesAsync();
        });

        await pg.RunAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM characters WHERE id = {character.Id}"));

        Assert.False(await pg.RunAsync(db => db.CharacterConditions.AnyAsync(c => c.CharacterId == character.Id)));
        Assert.False(await pg.RunAsync(db => db.GoldDeliveries.AnyAsync(d => d.CharacterId == character.Id)));
    }

    [Fact]
    public async Task There_is_one_spawn_at_most()
    {
        await AssertRefusedByAsync("ck_spawn_point_one", db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO spawn_point (id, area, x, y, z, yaw, set_by_ref, set_at)
            VALUES ({SpawnPoint.OnlyId + 1}, {"L_Tamriel"}, 0, 0, 0, 0, {"account:x"}, {Now})
            """));
    }

    // A raw write throws the database's refusal, a save through EF wraps it.
    private async Task AssertRefusedByAsync(string constraint, Func<CyrodiilContext, Task> write)
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(write));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal(constraint, refusal.ConstraintName);
    }
}
