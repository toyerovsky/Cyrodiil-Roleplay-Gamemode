using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Bank;

// A deposit and a withdrawal run on the writer, one job at a time, so each reads the balance the one before it left.
public static class BankBook
{
    // 0 for a character that never put anything in.
    public static async Task<long> FindBalanceAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        await db.BankAccounts.AsNoTracking().Where(a => a.CharacterId == characterId).Select(a => (long?)a.Balance).FirstOrDefaultAsync(ct) ?? 0;

    // The balance after it.
    public static async Task<long> DepositAsync(CyrodiilContext db, long characterId, int amount, DateTimeOffset at, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var account = await db.BankAccounts.FirstOrDefaultAsync(a => a.CharacterId == characterId, ct);
        if (account is null)
        {
            account = new BankAccount { CharacterId = characterId };
            db.BankAccounts.Add(account);
        }

        account.Balance = checked(account.Balance + amount);
        account.UpdatedAt = at;
        await db.SaveChangesAsync(ct);
        return account.Balance;
    }

    // An account that holds less than the amount is left as it is.
    public static async Task<BankWithdrawal> WithdrawAsync(CyrodiilContext db, long characterId, int amount, DateTimeOffset at, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var account = await db.BankAccounts.FirstOrDefaultAsync(a => a.CharacterId == characterId, ct);
        if (account is null || account.Balance < amount)
            return new BankWithdrawal(false, account?.Balance ?? 0);

        account.Balance -= amount;
        account.UpdatedAt = at;
        await db.SaveChangesAsync(ct);
        return new BankWithdrawal(true, account.Balance);
    }
}
