using CyrodiilRP.Data;
using CyrodiilRP.Data.Fishing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Tests.Database;

public sealed class FishingBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string Worms = "Worms";
    private const string Pike = "Pike";

    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_character_who_never_fished_has_no_row_and_no_records()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        Assert.Null(await FindAsync(character.Id));
        Assert.Empty(await ListAsync(character.Id));
    }

    [Fact]
    public async Task The_progress_is_written_whole_and_a_later_write_takes_its_place()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await SaveProgressAsync(character.Id, level: 3, xp: 12, catches: 5, Worms);
        await SaveProgressAsync(character.Id, level: 4, xp: 2, catches: 6, "None");

        var row = Assert.IsType<CharacterFishing>(await FindAsync(character.Id));
        Assert.Equal(4, row.Level);
        Assert.Equal(2, row.Xp);
        Assert.Equal(6, row.Catches);
        Assert.Equal("None", row.Bait);
        Assert.Equal(Now, row.UpdatedAt);
        Assert.Equal(1, await pg.RunAsync(db => db.CharacterFishing.CountAsync(f => f.CharacterId == character.Id)));
    }

    [Fact]
    public async Task A_record_is_kept_per_species_and_a_later_write_takes_its_place()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await SaveRecordAsync(character.Id, Pike, length: 60, grams: 1800, count: 1);
        await SaveRecordAsync(character.Id, "Perch", length: 20, grams: 100, count: 1);
        await SaveRecordAsync(character.Id, Pike, length: 74, grams: 3400, count: 2);

        var records = await ListAsync(character.Id);
        Assert.Equal(["Perch", Pike], records.Select(r => r.Species));
        var pike = records.Single(r => r.Species == Pike);
        Assert.Equal(74, pike.BestLengthCm);
        Assert.Equal(3400, pike.BestGrams);
        Assert.Equal(2, pike.Count);
        Assert.Equal(Now, pike.BestAt);
    }

    [Fact]
    public async Task One_characters_fishing_is_not_anothers()
    {
        var angler = await Seeds.CreateCharacterAsync(pg.Factory);
        var other = await Seeds.CreateCharacterAsync(pg.Factory);

        await SaveProgressAsync(angler.Id, level: 2, xp: 1, catches: 1, Worms);
        await SaveRecordAsync(angler.Id, Pike, length: 50, grams: 900, count: 1);

        Assert.Null(await FindAsync(other.Id));
        Assert.Empty(await ListAsync(other.Id));
    }

    [Fact]
    public async Task The_book_refuses_a_level_out_of_its_span_and_counts_below_zero()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => SaveProgressAsync(character.Id, FishingBook.LowestLevel - 1, 0, 0, Worms));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => SaveProgressAsync(character.Id, FishingBook.HighestLevel + 1, 0, 0, Worms));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => SaveProgressAsync(character.Id, 1, -1, 0, Worms));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => SaveRecordAsync(character.Id, Pike, length: 0, grams: 1, count: 1));
        Assert.Null(await FindAsync(character.Id));
    }

    [Fact]
    public async Task The_database_refuses_a_level_out_of_its_span_and_an_empty_record()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await SaveProgressAsync(character.Id, level: 1, xp: 0, catches: 0, Worms);
        await SaveRecordAsync(character.Id, Pike, length: 50, grams: 900, count: 1);

        await AssertRefusedByAsync("ck_character_fishing_level", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE character_fishing SET level = 101 WHERE character_id = {character.Id}"));
        await AssertRefusedByAsync("ck_character_fishing_counts", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE character_fishing SET xp = -1 WHERE character_id = {character.Id}"));
        await AssertRefusedByAsync("ck_character_fish_records_best", db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE character_fish_records SET count = 0 WHERE character_id = {character.Id}"));
    }

    [Fact]
    public async Task A_deleted_character_takes_its_fishing_along()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        await SaveProgressAsync(character.Id, level: 2, xp: 1, catches: 1, Worms);
        await SaveRecordAsync(character.Id, Pike, length: 50, grams: 900, count: 1);

        await pg.RunAsync(db => db.Characters.Where(c => c.Id == character.Id).ExecuteDeleteAsync());

        Assert.Null(await FindAsync(character.Id));
        Assert.Empty(await ListAsync(character.Id));
    }

    private Task<CharacterFishing?> FindAsync(long characterId) => pg.RunAsync(db => FishingBook.FindAsync(db, characterId, default));

    private Task<List<CharacterFishRecord>> ListAsync(long characterId) => pg.RunAsync(db => FishingBook.ListRecordsAsync(db, characterId, default));

    private Task SaveProgressAsync(long characterId, int level, int xp, int catches, string bait) =>
        pg.RunAsync(db => FishingBook.SaveProgressAsync(db, characterId, level, xp, catches, bait, Now, default));

    private Task SaveRecordAsync(long characterId, string species, int length, int grams, int count) =>
        pg.RunAsync(db => FishingBook.SaveRecordAsync(db, characterId, species, length, grams, count, Now, default));

    private async Task AssertRefusedByAsync(string constraint, Func<CyrodiilContext, Task> write)
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => pg.RunAsync(write));
        var refusal = Assert.IsType<PostgresException>(thrown as PostgresException ?? thrown.InnerException);
        Assert.Equal(constraint, refusal.ConstraintName);
    }
}
