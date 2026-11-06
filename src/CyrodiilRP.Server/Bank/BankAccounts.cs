using CyrodiilRP.Data.Bank;
using CyrodiilRP.Server.Infrastructure;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Bank;

// The gold characters keep at the bank. The database holds every balance and nothing of it is kept in memory, so what
// a caller is told is what was written. Whatever pays into the bank or from it goes through here.
[Service]
internal sealed partial class BankAccounts(Persistence persistence, SystemClock clock)
{
    private const string DepositJob = "bank.deposit";
    private const string WithdrawJob = "bank.withdraw";

    public Task<long> ReadBalanceAsync(long characterId) =>
        persistence.Reader.RunAsync((db, ct) => BankBook.FindBalanceAsync(db, characterId, ct));

    // The balance after it.
    public Task<long> DepositAsync(long characterId, int amount)
    {
        var at = clock.UtcNow;
        return persistence.Writer.EnqueueAsync(DepositJob, (db, ct) => BankBook.DepositAsync(db, characterId, amount, at, ct));
    }

    public Task<BankWithdrawal> WithdrawAsync(long characterId, int amount)
    {
        var at = clock.UtcNow;
        return persistence.Writer.EnqueueAsync(WithdrawJob, (db, ct) => BankBook.WithdrawAsync(db, characterId, amount, at, ct));
    }
}
