using CyrodiilRP.Data;
using CyrodiilRP.Data.Conditions;
using CyrodiilRP.Data.Sanctions;

namespace CyrodiilRP.Tests.Database;

public sealed class ActiveSanctionsTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string Mute = "admin.mute";
    private const string Freeze = "admin.freeze";
    private const string Wound = "body.wound";

    private static readonly DateTimeOffset Now = Seeds.Now;
    private static readonly string[] Keys = [Mute, Freeze];

    [Fact]
    public async Task Bans_in_force_come_with_their_end_and_one_for_good_with_none()
    {
        var banned = await Seeds.CreateAccountAsync(pg.Factory);
        var forever = await Seeds.CreateAccountAsync(pg.Factory);
        var served = await Seeds.CreateAccountAsync(pg.Factory);
        await pg.RunAsync(db => SanctionBook.BanAsync(db, banned.ReadyMId, Now.AddDays(1), "Spam", default));
        await pg.RunAsync(db => SanctionBook.BanAsync(db, forever.ReadyMId, SanctionBook.Forever, "Cheating", default));
        await pg.RunAsync(db => SanctionBook.BanAsync(db, served.ReadyMId, Now.AddDays(-1), "Old", default));

        var rows = await LoadActiveAsync();

        Assert.Equal(Now.AddDays(1), FilterBy(rows, banned.ReadyMId).Single().Until);
        var lasting = FilterBy(rows, forever.ReadyMId).Single();
        Assert.Equal(SanctionRow.Ban, lasting.Kind);
        Assert.Null(lasting.Until);
        Assert.Equal("Cheating", lasting.Reason);
        Assert.Empty(FilterBy(rows, served.ReadyMId));
    }

    [Fact]
    public async Task Only_the_conditions_asked_for_and_in_force_come_with_the_character()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var character = await Seeds.CreateCharacterAsync(pg.Factory, account.Id);
        await SaveAsync(character.Id, Mute, null);
        await SaveAsync(character.Id, Freeze, Now.AddMinutes(-1));
        await SaveAsync(character.Id, Wound, null);

        var row = FilterBy(await LoadActiveAsync(), account.ReadyMId).Single();

        Assert.Equal(Mute, row.Kind);
        Assert.Equal($"{character.FirstName} {character.LastName}", row.Character);
        Assert.Equal("account:staff", row.ByRef);
        Assert.Null(row.Until);
    }

    [Fact]
    public async Task A_condition_of_a_player_away_is_lifted_through_the_account()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var character = await Seeds.CreateCharacterAsync(pg.Factory, account.Id);
        await SaveAsync(character.Id, Freeze, null);
        await SaveAsync(character.Id, Mute, null);

        Assert.True(await pg.RunAsync(db => ConditionBook.RemoveForAccountAsync(db, account.ReadyMId, Freeze, default)));
        Assert.False(await pg.RunAsync(db => ConditionBook.RemoveForAccountAsync(db, account.ReadyMId, Freeze, default)));

        Assert.Equal([Mute], FilterBy(await LoadActiveAsync(), account.ReadyMId).Select(r => r.Kind));
    }

    private Task<List<SanctionRow>> LoadActiveAsync() => pg.RunAsync(db => SanctionBook.LoadActiveAsync(db, Keys, Now, default));

    private Task SaveAsync(long characterId, string key, DateTimeOffset? expiresAt) => pg.RunAsync(db => ConditionBook.SaveAsync(db, new CharacterCondition
    {
        CharacterId = characterId,
        Key = key,
        AppliedAt = Now.AddMinutes(-5),
        ExpiresAt = expiresAt,
        ByRef = "account:staff",
        Reason = "Reason",
    }, default));

    private static IEnumerable<SanctionRow> FilterBy(IEnumerable<SanctionRow> rows, Guid readyMId) => rows.Where(r => r.ReadyMId == readyMId);
}
