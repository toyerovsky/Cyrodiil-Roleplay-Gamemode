using CyrodiilRP.Data;
using CyrodiilRP.Data.Specializations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class SpecializationBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_character_who_never_chose_has_no_specialization()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Null(await FindAsync(character.Id));
    }

    [Fact]
    public async Task The_first_choice_is_kept_with_its_price_and_time_and_no_change()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await ChooseAsync(character.Id, SpecializationKind.Wizard, 0, Now);

        var row = Assert.IsType<CharacterSpecialization>(await FindAsync(character.Id));
        Assert.Equal(SpecializationKind.Wizard, row.Specialization);
        Assert.Equal(0, row.Price);
        Assert.Equal(Now, row.ChosenAt);
        Assert.Equal(0, row.Changes);
    }

    [Fact]
    public async Task A_change_replaces_the_choice_and_counts_one_change_each_time()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await ChooseAsync(character.Id, SpecializationKind.Knight, 0, Now);

        await ChooseAsync(character.Id, SpecializationKind.Thief, 1000, Now.AddDays(4));
        await ChooseAsync(character.Id, SpecializationKind.Healer, 1000, Now.AddDays(8));

        var row = (await FindAsync(character.Id))!;
        Assert.Equal(SpecializationKind.Healer, row.Specialization);
        Assert.Equal(1000, row.Price);
        Assert.Equal(Now.AddDays(8), row.ChosenAt);
        Assert.Equal(2, row.Changes);
        Assert.Equal(1, await pg.RunAsync(db => db.CharacterSpecializations.CountAsync(s => s.CharacterId == character.Id)));
    }

    [Fact]
    public async Task One_characters_specialization_is_not_anothers()
    {
        var chooser = await Seeds.CreateCharacterAsync(pg.Factory);
        var other = await Seeds.CreateCharacterAsync(pg.Factory);

        await ChooseAsync(chooser.Id, SpecializationKind.Smith, 0, Now);

        Assert.Null(await FindAsync(other.Id));
    }

    [Fact]
    public async Task A_specialization_is_stored_by_its_name()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await ChooseAsync(character.Id, SpecializationKind.Illusionist, 0, Now);

        var stored = await pg.RunAsync(db => db.Database
            .SqlQuery<string>($"SELECT specialization AS \"Value\" FROM character_specializations WHERE character_id = {character.Id}")
            .SingleAsync());
        Assert.Equal(nameof(SpecializationKind.Illusionist), stored);
    }

    [Fact]
    public async Task No_price_below_nothing_reaches_the_table()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await ChooseAsync(character.Id, SpecializationKind.Ranger, 0, Now);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ChooseAsync(character.Id, SpecializationKind.Knight, -1, Now));

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE character_specializations SET price = -1 WHERE character_id = {character.Id}")));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal("ck_character_specializations_price", refusal.ConstraintName);
        Assert.Equal(SpecializationKind.Ranger, (await FindAsync(character.Id))!.Specialization);
    }

    [Fact]
    public async Task A_deleted_character_takes_its_specialization_along()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await ChooseAsync(character.Id, SpecializationKind.Barbarian, 0, Now);

        await pg.RunAsync(db => db.Characters.Where(c => c.Id == character.Id).ExecuteDeleteAsync());

        Assert.False(await pg.RunAsync(db => db.CharacterSpecializations.AnyAsync(s => s.CharacterId == character.Id)));
    }

    [Fact]
    public async Task The_staff_set_a_specialization_for_free_and_keep_the_time_of_the_last_choice()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await ChooseAsync(character.Id, SpecializationKind.Knight, 1000, Now);

        await pg.RunAsync(db => SpecializationBook.SetByStaffAsync(db, character.Id, SpecializationKind.Thief, Now.AddDays(1), default));

        var row = (await FindAsync(character.Id))!;
        Assert.Equal(SpecializationKind.Thief, row.Specialization);
        Assert.Equal(0, row.Price);
        Assert.Equal(Now, row.ChosenAt);
        Assert.Equal(0, row.Changes);
    }

    [Fact]
    public async Task The_staff_give_a_first_specialization_or_take_one_away()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await pg.RunAsync(db => SpecializationBook.SetByStaffAsync(db, character.Id, SpecializationKind.Healer, Now, default));
        Assert.Equal(SpecializationKind.Healer, (await FindAsync(character.Id))!.Specialization);

        await pg.RunAsync(db => SpecializationBook.SetByStaffAsync(db, character.Id, null, Now, default));
        Assert.Null(await FindAsync(character.Id));
        await pg.RunAsync(db => SpecializationBook.SetByStaffAsync(db, character.Id, null, Now, default));
        Assert.Null(await FindAsync(character.Id));
    }

    [Fact]
    public async Task A_cap_override_is_set_changed_and_taken_off_per_stat_and_character()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        var other = await Seeds.CreateCharacterAsync(pg.Factory);

        await SetOverrideAsync(character.Id, "Blade", 80);
        await SetOverrideAsync(character.Id, "Sneak", 0);
        await SetOverrideAsync(character.Id, "Blade", 90);
        await SetOverrideAsync(other.Id, "Blade", 10);

        var overrides = await pg.RunAsync(db => SpecializationBook.LoadOverridesAsync(db, character.Id, default));
        Assert.Equal(["Blade:90", "Sneak:0"], overrides.Select(o => $"{o.Stat}:{o.Cap}"));

        await SetOverrideAsync(character.Id, "Blade", null);
        await SetOverrideAsync(character.Id, "Luck", null);
        overrides = await pg.RunAsync(db => SpecializationBook.LoadOverridesAsync(db, character.Id, default));
        Assert.Equal(["Sneak:0"], overrides.Select(o => $"{o.Stat}:{o.Cap}"));
        Assert.Single(await pg.RunAsync(db => SpecializationBook.LoadOverridesAsync(db, other.Id, default)));
    }

    [Fact]
    public async Task No_cap_outside_0_to_100_reaches_the_table_and_a_deleted_character_takes_its_caps_along()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await SetOverrideAsync(character.Id, "Blade", 100);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => SetOverrideAsync(character.Id, "Blade", 101));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => SetOverrideAsync(character.Id, "Blade", -1));
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE character_cap_overrides SET cap = 101 WHERE character_id = {character.Id}")));
        Assert.Equal("ck_character_cap_overrides_cap", Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException).ConstraintName);

        await pg.RunAsync(db => db.Characters.Where(c => c.Id == character.Id).ExecuteDeleteAsync());
        Assert.False(await pg.RunAsync(db => db.CharacterCapOverrides.AnyAsync(o => o.CharacterId == character.Id)));
    }

    private Task SetOverrideAsync(long characterId, string stat, int? cap) =>
        pg.RunAsync(db => SpecializationBook.SetOverrideAsync(db, characterId, stat, cap, default));

    private Task<CharacterSpecialization?> FindAsync(long characterId) =>
        pg.RunAsync(db => SpecializationBook.FindAsync(db, characterId, default));

    private Task ChooseAsync(long characterId, SpecializationKind kind, int price, DateTimeOffset at) =>
        pg.RunAsync(db => SpecializationBook.ChooseAsync(db, characterId, kind, price, at, default));
}
