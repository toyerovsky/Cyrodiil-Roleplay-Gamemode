using CyrodiilRP.Data.Characters;
using CyrodiilRP.Data.Fishing;
using CyrodiilRP.Data.Lore;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

// The class's database is one for all its tests, so each test fishes a species of its own and reads only that.
public sealed class FishingBookBoardTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const int PerSpecies = 3;
    private const int Grams = 900;

    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task The_board_lists_the_longest_of_a_species_the_best_first_with_whose_they_are()
    {
        const string species = "Pike";
        var shorter = await Seeds.CreateCharacterAsync(pg.Factory);
        var longest = await Seeds.CreateCharacterAsync(pg.Factory);
        var middle = await Seeds.CreateCharacterAsync(pg.Factory);
        await SaveAsync(shorter.Id, species, length: 60, Now);
        await SaveAsync(longest.Id, species, length: 90, Now.AddMinutes(1));
        await SaveAsync(middle.Id, species, length: 74, Now.AddMinutes(2));

        var top = await ReadTopAsync(species, PerSpecies);

        Assert.Equal([longest.Id, middle.Id, shorter.Id], top.Select(row => row.CharacterId));
        var first = top[0];
        Assert.Equal(longest.FirstName, first.FirstName);
        Assert.Equal(longest.LastName, first.LastName);
        Assert.False(first.NamePublic);
        Assert.Equal(90, first.BestLengthCm);
        Assert.Equal(Grams, first.BestGrams);
        Assert.Equal(Now.AddMinutes(1), first.BestAt);
    }

    [Fact]
    public async Task Fish_of_one_length_stand_in_the_order_they_were_caught()
    {
        const string species = "Carp";
        var later = await Seeds.CreateCharacterAsync(pg.Factory);
        var earlier = await Seeds.CreateCharacterAsync(pg.Factory);
        var longer = await Seeds.CreateCharacterAsync(pg.Factory);
        await SaveAsync(later.Id, species, length: 50, Now.AddHours(2));
        await SaveAsync(earlier.Id, species, length: 50, Now.AddHours(1));
        await SaveAsync(longer.Id, species, length: 51, Now.AddHours(3));

        var top = await ReadTopAsync(species, PerSpecies);

        Assert.Equal([longer.Id, earlier.Id, later.Id], top.Select(row => row.CharacterId));
    }

    [Fact]
    public async Task The_board_keeps_so_many_of_a_species_and_counts_every_best_length()
    {
        const string species = "Trout";
        var lengths = new[] { 30, 44, 44, 52, 38 };
        var anglers = new List<CharacterSnapshot>();
        foreach (var length in lengths)
        {
            var angler = await Seeds.CreateCharacterAsync(pg.Factory);
            anglers.Add(angler);
            await SaveAsync(angler.Id, species, length, Now.AddMinutes(anglers.Count));
        }

        var board = await ReadAsync(PerSpecies);

        Assert.Equal([anglers[3].Id, anglers[1].Id, anglers[2].Id], board.Top.Where(row => row.Species == species).Select(row => row.CharacterId));
        var counted = board.Lengths.Where(count => count.Species == species).OrderBy(count => count.LengthCm).ToList();
        Assert.Equal([30, 38, 44, 52], counted.Select(count => count.LengthCm));
        Assert.Equal([1, 1, 2, 1], counted.Select(count => count.Characters));
    }

    [Fact]
    public async Task Each_species_has_a_top_of_its_own()
    {
        const string eel = "Eel";
        const string betty = "Betty";
        var angler = await Seeds.CreateCharacterAsync(pg.Factory);
        var other = await Seeds.CreateCharacterAsync(pg.Factory);
        await SaveAsync(angler.Id, eel, length: 80, Now);
        await SaveAsync(other.Id, eel, length: 85, Now);
        await SaveAsync(angler.Id, betty, length: 15, Now);

        var board = await ReadAsync(perSpecies: 1);

        Assert.Equal(other.Id, Assert.Single(board.Top, row => row.Species == eel).CharacterId);
        Assert.Equal(angler.Id, Assert.Single(board.Top, row => row.Species == betty).CharacterId);
        Assert.Equal(board.Top.OrderBy(row => row.Species, StringComparer.Ordinal).Select(row => row.Species), board.Top.Select(row => row.Species));
    }

    [Fact]
    public async Task The_board_says_whose_name_everyone_may_read()
    {
        const string species = "Longfin";
        var open = await Seeds.CreateCharacterAsync(pg.Factory);
        var quiet = await Seeds.CreateCharacterAsync(pg.Factory);
        await pg.RunAsync(db => LoreBook.SetNamePublicAsync(db, open.Id, true, default));
        await SaveAsync(open.Id, species, length: 70, Now);
        await SaveAsync(quiet.Id, species, length: 60, Now);

        var top = await ReadTopAsync(species, PerSpecies);

        Assert.Equal([true, false], top.Select(row => row.NamePublic));
    }

    [Fact]
    public async Task A_deleted_character_leaves_the_board()
    {
        const string species = "Seabass";
        var gone = await Seeds.CreateCharacterAsync(pg.Factory);
        var kept = await Seeds.CreateCharacterAsync(pg.Factory);
        await SaveAsync(gone.Id, species, length: 70, Now);
        await SaveAsync(kept.Id, species, length: 60, Now);

        await pg.RunAsync(db => db.Characters.Where(c => c.Id == gone.Id).ExecuteDeleteAsync());

        var board = await ReadAsync(PerSpecies);
        Assert.Equal(kept.Id, Assert.Single(board.Top, row => row.Species == species).CharacterId);
        Assert.Equal(60, Assert.Single(board.Lengths, count => count.Species == species).LengthCm);
    }

    [Fact]
    public async Task The_board_refuses_a_top_of_none()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ReadAsync(perSpecies: 0));
    }

    private Task<FishBoard> ReadAsync(int perSpecies) => pg.RunAsync(db => FishingBook.ReadBoardAsync(db, perSpecies, default));

    private async Task<List<FishBoardRow>> ReadTopAsync(string species, int perSpecies) =>
        [.. (await ReadAsync(perSpecies)).Top.Where(row => row.Species == species)];

    private Task SaveAsync(long characterId, string species, int length, DateTimeOffset at) =>
        pg.RunAsync(db => FishingBook.SaveRecordAsync(db, characterId, species, length, Grams, count: 1, at, default));
}
