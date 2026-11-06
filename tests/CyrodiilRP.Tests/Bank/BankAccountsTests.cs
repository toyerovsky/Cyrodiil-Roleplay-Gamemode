using CyrodiilRP.Data.Bank;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Bank;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Tests.Database;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Bank;

public sealed class BankAccountsTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Persistence _persistence = null!;
    private BankAccounts _accounts = null!;

    public Task InitializeAsync()
    {
        _persistence = new Persistence(new RpConfig { Database = { ConnectionString = pg.ConnectionString } }, new ListLogger());
        _accounts = new BankAccounts(_persistence, new SystemClock());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _persistence.Writer.DisposeAsync();

    [Fact]
    public async Task Moves_queued_together_are_written_in_the_order_they_were_made()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        var first = _accounts.DepositAsync(character.Id, 100);
        var tooMuch = _accounts.WithdrawAsync(character.Id, 150);
        var second = _accounts.DepositAsync(character.Id, 100);
        var taken = _accounts.WithdrawAsync(character.Id, 150);

        Assert.Equal(100, await first);
        Assert.Equal(new BankWithdrawal(false, 100), await tooMuch);
        Assert.Equal(200, await second);
        Assert.Equal(new BankWithdrawal(true, 50), await taken);
        Assert.Equal(50, await _accounts.ReadBalanceAsync(character.Id));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(BankRules.MostAtOnce, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(BankRules.MostAtOnce + 1, false)]
    public void One_move_takes_from_one_gold_to_the_most_at_once(int amount, bool fits) => Assert.Equal(fits, BankRules.Fits(amount));
}
