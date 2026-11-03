using CyrodiilRP.Data;
using CyrodiilRP.Data.Characters;
using CyrodiilRP.Data.Conditions;
using CyrodiilRP.Data.Staff;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class CharacterBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const int StartingGold = 25;
    private const string Mute = "admin.mute";

    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_first_join_makes_the_account_and_later_joins_find_it()
    {
        var player = Guid.NewGuid();

        var first = await pg.RunAsync(db => CharacterBook.JoinAsync(db, player, Now, default));
        var second = await pg.RunAsync(db => CharacterBook.JoinAsync(db, player, Now.AddHours(1), default));

        Assert.Equal(first.AccountId, second.AccountId);
        Assert.Null(second.Character);
        var account = await pg.RunAsync(db => db.Accounts.SingleAsync(a => a.ReadyMId == player));
        Assert.Equal(Now, account.CreatedAt);
        Assert.Equal(Now.AddHours(1), account.LastSeenAt);
    }

    [Fact]
    public async Task A_join_reads_the_accounts_language_and_roles_from_the_database()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        await pg.RunAsync(db => StaffBook.SetRolesAsync(db, account.ReadyMId, [StaffRole.Moderator], default));
        await pg.RunAsync(db => db.Accounts.Where(a => a.Id == account.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Locale, "pl")));

        var joined = await pg.RunAsync(db => CharacterBook.JoinAsync(db, account.ReadyMId, Now, default));

        Assert.Equal("pl", joined.Locale);
        Assert.Equal([StaffRole.Moderator], joined.Roles);
    }

    [Fact]
    public async Task A_join_brings_the_characters_conditions_that_have_not_run_out()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var character = await Seeds.CreateCharacterAsync(pg.Factory, account.Id);
        await pg.RunAsync(db => ConditionBook.SaveAsync(db, CreateCondition(character.Id, Mute, Now.AddMinutes(5)), default));
        await pg.RunAsync(db => ConditionBook.SaveAsync(db, CreateCondition(character.Id, "test.over", Now), default));

        var joined = await pg.RunAsync(db => CharacterBook.JoinAsync(db, account.ReadyMId, Now, default));

        Assert.Equal([Mute], joined.Conditions.Select(c => c.Key));
    }

    [Fact]
    public async Task A_created_character_comes_back_at_the_next_join()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var first = Names.CreateUnique();
        var last = Names.CreateUnique();

        var created = await pg.RunAsync(db => CharacterBook.CreateAsync(db, account.Id, first, last, StartingGold, Now, default));
        var joined = await pg.RunAsync(db => CharacterBook.JoinAsync(db, account.ReadyMId, Now, default));

        Assert.Equal(CreateOutcome.Created, created.Outcome);
        Assert.Equal(new CharacterSnapshot(created.Character!.Id, first, last, Now, Now, 0), joined.Character);
    }

    [Fact]
    public async Task A_join_tells_when_the_account_was_here_before_it()
    {
        var player = Guid.NewGuid();

        var first = await pg.RunAsync(db => CharacterBook.JoinAsync(db, player, Now, default));
        var second = await pg.RunAsync(db => CharacterBook.JoinAsync(db, player, Now.AddDays(2), default));

        Assert.Null(first.PreviousVisitAt);
        Assert.Equal(Now, second.PreviousVisitAt);
        Assert.Equal(Now, second.AccountCreatedAt);
    }

    [Fact]
    public async Task The_time_played_comes_back_with_the_character()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var character = await Seeds.CreateCharacterAsync(pg.Factory, account.Id);
        await pg.RunAsync(db => CharacterBook.RecordPlayAsync(db, character.Id, Now, 90, default));
        await pg.RunAsync(db => CharacterBook.RecordPlayAsync(db, character.Id, Now, 30, default));

        var joined = await pg.RunAsync(db => CharacterBook.JoinAsync(db, account.ReadyMId, Now, default));

        Assert.Equal(120, joined.Character!.PlayedSeconds);
    }

    [Fact]
    public async Task A_join_tells_when_the_character_was_last_played()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var character = await Seeds.CreateCharacterAsync(pg.Factory, account.Id);
        var played = Now.AddDays(2);
        await pg.RunAsync(db => CharacterBook.RecordPlayAsync(db, character.Id, played, 60, default));

        var joined = await pg.RunAsync(db => CharacterBook.JoinAsync(db, account.ReadyMId, played.AddDays(1), default));

        Assert.Equal(played, joined.Character!.LastPlayedAt);
    }

    [Fact]
    public async Task A_chosen_language_is_the_accounts_at_the_next_join()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        await pg.RunAsync(db => CharacterBook.SetLocaleAsync(db, account.Id, "pl", default));

        Assert.Equal("pl", (await pg.RunAsync(db => CharacterBook.JoinAsync(db, account.ReadyMId, Now, default))).Locale);
    }

    [Fact]
    public async Task A_name_belongs_to_one_character_whatever_its_letter_case()
    {
        var taken = await Seeds.CreateCharacterAsync(pg.Factory);
        var other = await Seeds.CreateAccountAsync(pg.Factory);

        var refused = await pg.RunAsync(db =>
            CharacterBook.CreateAsync(db, other.Id, taken.FirstName.ToUpperInvariant(), taken.LastName.ToLowerInvariant(), StartingGold, Now, default));

        Assert.Equal(CreateOutcome.NameTaken, refused.Outcome);
        Assert.Null(refused.Character);
        Assert.False(await pg.RunAsync(db => db.Characters.AnyAsync(c => c.AccountId == other.Id)));
    }

    [Fact]
    public async Task An_account_has_one_character_and_hears_which()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var existing = await Seeds.CreateCharacterAsync(pg.Factory, account.Id);

        var second = await pg.RunAsync(db => CharacterBook.CreateAsync(db, account.Id, Names.CreateUnique(), Names.CreateUnique(), StartingGold, Now, default));

        Assert.Equal(CreateOutcome.AlreadyHasCharacter, second.Outcome);
        Assert.Equal(existing, second.Character);
        Assert.False(await pg.RunAsync(db => db.GoldDeliveries.AnyAsync(d => d.CharacterId == existing.Id)));
    }

    [Fact]
    public async Task A_new_character_is_owed_the_starting_gold_in_the_same_write_and_only_once()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        var created = await pg.RunAsync(db => CharacterBook.CreateAsync(db, account.Id, Names.CreateUnique(), Names.CreateUnique(), StartingGold, Now, default));
        await pg.RunAsync(db => CharacterBook.CreateAsync(db, account.Id, Names.CreateUnique(), Names.CreateUnique(), StartingGold, Now, default));

        var owed = Assert.Single(await pg.RunAsync(db => db.GoldDeliveries.Where(d => d.CharacterId == created.Character!.Id).ToListAsync()));
        Assert.Equal(StartingGold, owed.Amount);
        Assert.Equal(GoldDeliveryReason.StartingGold, owed.Reason);
        Assert.Null(owed.DeliveredAt);
    }

    [Fact]
    public async Task No_starting_gold_owes_nothing()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);

        var created = await pg.RunAsync(db => CharacterBook.CreateAsync(db, account.Id, Names.CreateUnique(), Names.CreateUnique(), 0, Now, default));

        Assert.False(await pg.RunAsync(db => db.GoldDeliveries.AnyAsync(d => d.CharacterId == created.Character!.Id)));
    }

    [Fact]
    public async Task A_name_is_taken_whatever_its_letter_case_and_a_free_one_is_not()
    {
        var taken = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.True(await pg.RunAsync(db => CharacterBook.IsNameTakenAsync(db, taken.FirstName.ToUpperInvariant(), taken.LastName.ToLowerInvariant(), default)));
        Assert.False(await pg.RunAsync(db => CharacterBook.IsNameTakenAsync(db, Names.CreateUnique(), Names.CreateUnique(), default)));
    }

    [Fact]
    public async Task Time_played_adds_up_and_never_goes_back()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await pg.RunAsync(db => CharacterBook.RecordPlayAsync(db, character.Id, Now.AddMinutes(30), 1800, default));
        await pg.RunAsync(db => CharacterBook.RecordPlayAsync(db, character.Id, Now.AddMinutes(40), -60, default));

        var row = await pg.RunAsync(db => db.Characters.SingleAsync(c => c.Id == character.Id));
        Assert.Equal(1800, row.PlayedSeconds);
        Assert.Equal(Now.AddMinutes(40), row.LastPlayedAt);
    }

    [Fact]
    public async Task The_accounts_nickname_is_remembered_and_cut_to_its_column()
    {
        var account = await Seeds.CreateAccountAsync(pg.Factory);
        var nickname = new string('n', ColumnLengths.Nickname + 10);

        await pg.RunAsync(db => CharacterBook.RememberNicknameAsync(db, account.Id, nickname, default));

        Assert.Equal(nickname[..ColumnLengths.Nickname], (await pg.RunAsync(db => db.Accounts.SingleAsync(a => a.Id == account.Id))).LastNickname);
    }

    private static CharacterCondition CreateCondition(long characterId, string key, DateTimeOffset? expiresAt) => new()
    {
        CharacterId = characterId,
        Key = key,
        AppliedAt = Now,
        ExpiresAt = expiresAt,
    };
}
