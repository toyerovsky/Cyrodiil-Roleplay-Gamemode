using CyrodiilRP.Data.Characters;
using CyrodiilRP.Data.Economy;

namespace CyrodiilRP.Tests.Database;

public sealed class GoldDeliveryBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const int StartingGold = 40;

    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task Owed_gold_is_claimed_once()
    {
        var owed = await OweGoldAsync();

        Assert.True(await pg.RunAsync(db => GoldDeliveryBook.ClaimAsync(db, owed.Delivery.Id, Now, default)));
        Assert.False(await pg.RunAsync(db => GoldDeliveryBook.ClaimAsync(db, owed.Delivery.Id, Now, default)));
        Assert.Empty(await pg.RunAsync(db => GoldDeliveryBook.LoadPendingAsync(db, owed.CharacterId, default)));
    }

    [Fact]
    public async Task Released_gold_is_owed_again()
    {
        var owed = await OweGoldAsync();
        await pg.RunAsync(db => GoldDeliveryBook.ClaimAsync(db, owed.Delivery.Id, Now, default));

        await pg.RunAsync(db => GoldDeliveryBook.ReleaseAsync(db, owed.Delivery.Id, default));

        Assert.Equal([owed.Delivery], await pg.RunAsync(db => GoldDeliveryBook.LoadPendingAsync(db, owed.CharacterId, default)));
    }

    [Fact]
    public async Task Another_characters_gold_is_not_theirs()
    {
        await OweGoldAsync();
        var other = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Empty(await pg.RunAsync(db => GoldDeliveryBook.LoadPendingAsync(db, other.Id, default)));
    }

    private async Task<OwedGold> OweGoldAsync()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var created = await pg.RunAsync(db => CharacterBook.CreateAsync(db, account.Id, Names.CreateUnique(), Names.CreateUnique(), StartingGold, Now, default));
        var characterId = created.Character!.Id;
        var delivery = Assert.Single(await pg.RunAsync(db => GoldDeliveryBook.LoadPendingAsync(db, characterId, default)));
        Assert.Equal(StartingGold, delivery.Amount);
        return new OwedGold(characterId, delivery);
    }

    private sealed record OwedGold(long CharacterId, PendingGold Delivery);
}
