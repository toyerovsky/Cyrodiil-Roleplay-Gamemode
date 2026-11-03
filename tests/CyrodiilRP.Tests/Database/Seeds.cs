using CyrodiilRP.Data;
using CyrodiilRP.Data.Characters;

namespace CyrodiilRP.Tests.Database;

internal static class Seeds
{
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    public static async Task<SeededAccount> CreateAccountAsync(CyrodiilContextFactory factory)
    {
        var readyMId = Guid.NewGuid();
        await using var db = factory.Create();
        var joined = await CharacterBook.JoinAsync(db, readyMId, Now, default);
        return new SeededAccount(joined.AccountId, readyMId);
    }

    public static async Task<CharacterSnapshot> CreateCharacterAsync(CyrodiilContextFactory factory, long? accountId = null)
    {
        var account = accountId ?? (await CreateAccountAsync(factory)).Id;
        await using var db = factory.Create();
        var created = await CharacterBook.CreateAsync(db, account, Names.CreateUnique(), Names.CreateUnique(), 0, Now, default);
        return created.Character ?? throw new InvalidOperationException($"No character made: {created.Outcome}.");
    }
}
