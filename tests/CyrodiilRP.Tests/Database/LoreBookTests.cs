using CyrodiilRP.Data;
using CyrodiilRP.Data.Lore;
using CyrodiilRP.Data.Staff;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class LoreBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string History = "history";
    private const string Lazy = "trait.lazy";

    [Fact]
    public async Task A_card_saves_each_field_once_and_keeps_its_visibility_when_cleared()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await pg.RunAsync(db => LoreBook.SaveFieldsAsync(db, character.Id, [Row(character.Id, History, "Born in Bravil.", LoreVisibility.Public)], default));
        await pg.RunAsync(db => LoreBook.SaveFieldsAsync(db, character.Id, [Row(character.Id, History, null, LoreVisibility.Public)], default));

        var card = await pg.RunAsync(db => LoreBook.LoadCardAsync(db, character.Id, default));
        var field = Assert.Single(card.Fields);
        Assert.Equal(History, field.FieldId);
        Assert.Null(field.Text);
        Assert.Equal(LoreVisibility.Public, field.Visibility);
    }

    [Fact]
    public async Task A_trait_keeps_its_number_and_rolls()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        var row = Row(character.Id, Lazy, null, LoreVisibility.Private);
        row.Number = 46;
        row.Rolls = "5d20:3,17,9,11,6=46";

        await pg.RunAsync(db => LoreBook.SaveFieldsAsync(db, character.Id, [row], default));

        var stored = Assert.Single((await pg.RunAsync(db => LoreBook.LoadCardAsync(db, character.Id, default))).Fields);
        Assert.Equal(46, stored.Number);
        Assert.Equal("5d20:3,17,9,11,6=46", stored.Rolls);
        Assert.Equal(LoreVisibility.Private, stored.Visibility);
    }

    [Fact]
    public async Task A_name_is_private_until_made_public()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        Assert.False((await pg.RunAsync(db => LoreBook.LoadCardAsync(db, character.Id, default))).NamePublic);

        await pg.RunAsync(db => LoreBook.SetNamePublicAsync(db, character.Id, true, default));

        Assert.True((await pg.RunAsync(db => LoreBook.LoadCardAsync(db, character.Id, default))).NamePublic);
    }

    [Fact]
    public async Task An_introduction_writes_both_directions_and_a_forgetting_takes_one()
    {
        var first = await Seeds.CreateCharacterAsync(pg.Factory);
        var second = await Seeds.CreateCharacterAsync(pg.Factory);

        await pg.RunAsync(db => LoreBook.IntroduceAsync(db, first.Id, second.Id, Seeds.Now, default));
        await pg.RunAsync(db => LoreBook.IntroduceAsync(db, second.Id, first.Id, Seeds.Now.AddHours(1), default));

        var known = await pg.RunAsync(db => LoreBook.LoadIntroductionsAsync(db, first.Id, default));
        Assert.Equal(2, known.Count);
        Assert.All(known, introduction => Assert.Equal(Seeds.Now, introduction.At));

        Assert.True(await pg.RunAsync(db => LoreBook.ForgetAsync(db, second.Id, first.Id, default)));
        Assert.False(await pg.RunAsync(db => LoreBook.ForgetAsync(db, second.Id, first.Id, default)));

        var left = Assert.Single(await pg.RunAsync(db => LoreBook.LoadIntroductionsAsync(db, second.Id, default)));
        Assert.Equal(first.Id, left.KnowerCharacterId);
        Assert.Equal(second.Id, left.KnownCharacterId);
    }

    [Fact]
    public async Task A_trait_number_stays_within_what_five_d20_give()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await pg.RunAsync(db => LoreBook.SaveFieldsAsync(db, character.Id, [Row(character.Id, Lazy, null, LoreVisibility.Private)], default));

        await AssertRefusedByAsync("ck_lore_fields_number", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE lore_fields SET number = 101 WHERE character_id = {character.Id}"));
    }

    [Fact]
    public async Task A_character_never_introduces_itself_to_itself()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await AssertRefusedByAsync("ck_lore_introductions_other", db =>
        {
            db.LoreIntroductions.Add(new LoreIntroduction { KnowerCharacterId = character.Id, KnownCharacterId = character.Id, At = Seeds.Now });
            return db.SaveChangesAsync();
        });
    }

    // /alore came after the roles were seeded: a database migrated to the end gives it to the moderators.
    [Fact]
    public async Task The_seeded_moderators_may_clear_a_lore_card()
    {
        var connectionString = await pg.CreateEmptyAsync();
        await using var db = new CyrodiilContextFactory(connectionString).Create();
        await db.Database.MigrateAsync();

        var roles = await db.StaffRoles.AsNoTracking().ToDictionaryAsync(role => role.Name);
        Assert.Contains(StaffPermissions.Lore, roles[StaffRole.Moderator].Permissions);
        Assert.Contains(StaffPermissions.Lore, roles[StaffRole.Admin].Permissions);
    }

    private static LoreField Row(long characterId, string fieldId, string? text, LoreVisibility visibility) => new()
    {
        CharacterId = characterId,
        FieldId = fieldId,
        Text = text,
        Visibility = visibility,
        UpdatedAt = Seeds.Now,
    };

    private async Task AssertRefusedByAsync(string constraint, Func<CyrodiilContext, Task> write)
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(write));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal(constraint, refusal.ConstraintName);
    }
}
