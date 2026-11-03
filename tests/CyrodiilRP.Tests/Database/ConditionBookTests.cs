using CyrodiilRP.Data;
using CyrodiilRP.Data.Conditions;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class ConditionBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string Mute = "admin.mute";
    private const string Wound = "body.wound";

    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_condition_saved_again_is_one_row_with_the_later_values()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await pg.RunAsync(db => ConditionBook.SaveAsync(db, CreateCondition(character.Id, Mute, Now.AddMinutes(5), "Spam"), default));
        await pg.RunAsync(db => ConditionBook.SaveAsync(db, CreateCondition(character.Id, Mute, Now.AddMinutes(30), "Spam again"), default));

        var stored = Assert.Single(await pg.RunAsync(db => db.CharacterConditions.Where(c => c.CharacterId == character.Id).ToListAsync()));
        Assert.Equal(Now.AddMinutes(30), stored.ExpiresAt);
        Assert.Equal("Spam again", stored.Reason);
    }

    [Fact]
    public async Task What_ran_out_is_not_active_and_waits_to_be_overwritten()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await pg.RunAsync(db => ConditionBook.SaveAsync(db, CreateCondition(character.Id, Mute, Now.AddMinutes(5), "Spam"), default));
        await pg.RunAsync(db => ConditionBook.SaveAsync(db, CreateCondition(character.Id, Wound, null, null), default));

        var active = await pg.RunAsync(db => ConditionBook.LoadActiveAsync(db, character.Id, Now.AddMinutes(5), default));

        Assert.Equal([Wound], active.Select(c => c.Key));
        Assert.Equal(2, await pg.RunAsync(db => db.CharacterConditions.CountAsync(c => c.CharacterId == character.Id)));
    }

    [Fact]
    public async Task A_removed_condition_is_gone_and_others_stay()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await pg.RunAsync(db => ConditionBook.SaveAsync(db, CreateCondition(character.Id, Mute, null, "Spam"), default));
        await pg.RunAsync(db => ConditionBook.SaveAsync(db, CreateCondition(character.Id, Wound, null, null), default));

        await pg.RunAsync(db => ConditionBook.RemoveAsync(db, character.Id, Mute, default));

        Assert.Equal([Wound], (await pg.RunAsync(db => ConditionBook.LoadActiveAsync(db, character.Id, Now, default))).Select(c => c.Key));
    }

    private static CharacterCondition CreateCondition(long characterId, string key, DateTimeOffset? expiresAt, string? reason) => new()
    {
        CharacterId = characterId,
        Key = key,
        AppliedAt = Now,
        ExpiresAt = expiresAt,
        ByRef = "account:staff",
        Reason = reason,
    };
}
