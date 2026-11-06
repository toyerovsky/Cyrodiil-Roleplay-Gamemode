using CyrodiilRP.Data.Bank;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class BankBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_character_who_never_put_anything_in_has_nothing_at_the_bank()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Equal(0, await FindBalanceAsync(character.Id));
    }

    [Fact]
    public async Task Deposits_add_up_and_each_says_the_balance_after_it()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Equal(120, await DepositAsync(character.Id, 120));
        Assert.Equal(200, await DepositAsync(character.Id, 80));
        Assert.Equal(200, await FindBalanceAsync(character.Id));
    }

    [Fact]
    public async Task A_withdrawal_takes_its_gold_and_says_what_is_left()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await DepositAsync(character.Id, 200);

        Assert.Equal(new BankWithdrawal(true, 50), await WithdrawAsync(character.Id, 150));
        Assert.Equal(new BankWithdrawal(true, 0), await WithdrawAsync(character.Id, 50));
        Assert.Equal(0, await FindBalanceAsync(character.Id));
    }

    [Fact]
    public async Task A_withdrawal_of_more_than_the_account_holds_leaves_it_as_it_is()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await DepositAsync(character.Id, 200);

        Assert.Equal(new BankWithdrawal(false, 200), await WithdrawAsync(character.Id, 201));
        Assert.Equal(200, await FindBalanceAsync(character.Id));
    }

    [Fact]
    public async Task A_withdrawal_from_no_account_takes_nothing()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Equal(new BankWithdrawal(false, 0), await WithdrawAsync(character.Id, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task An_amount_of_nothing_or_less_is_never_moved(int amount)
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => DepositAsync(character.Id, amount));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => WithdrawAsync(character.Id, amount));
    }

    [Fact]
    public async Task One_characters_gold_is_not_anothers()
    {
        var rich = await Seeds.CreateCharacterAsync(pg.Factory);
        var poor = await Seeds.CreateCharacterAsync(pg.Factory);
        await DepositAsync(rich.Id, 500);

        Assert.Equal(0, await FindBalanceAsync(poor.Id));
        Assert.Equal(new BankWithdrawal(false, 0), await WithdrawAsync(poor.Id, 100));
    }

    [Fact]
    public async Task A_balance_is_never_below_nothing()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await DepositAsync(character.Id, 10);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE bank_accounts SET balance = -1 WHERE character_id = {character.Id}")));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal("ck_bank_accounts_balance", refusal.ConstraintName);
    }

    [Fact]
    public async Task A_deleted_character_takes_its_account_along()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await DepositAsync(character.Id, 10);

        await pg.RunAsync(db => db.Characters.Where(c => c.Id == character.Id).ExecuteDeleteAsync());

        Assert.False(await pg.RunAsync(db => db.BankAccounts.AnyAsync(a => a.CharacterId == character.Id)));
    }

    private Task<long> FindBalanceAsync(long characterId) => pg.RunAsync(db => BankBook.FindBalanceAsync(db, characterId, default));

    private Task<long> DepositAsync(long characterId, int amount) => pg.RunAsync(db => BankBook.DepositAsync(db, characterId, amount, Now, default));

    private Task<BankWithdrawal> WithdrawAsync(long characterId, int amount) =>
        pg.RunAsync(db => BankBook.WithdrawAsync(db, characterId, amount, Now, default));
}
