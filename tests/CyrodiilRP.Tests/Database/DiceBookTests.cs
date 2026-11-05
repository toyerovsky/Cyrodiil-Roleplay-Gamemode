using CyrodiilRP.Data;
using CyrodiilRP.Data.Dice;

namespace CyrodiilRP.Tests.Database;

public sealed class DiceBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const int Recent = 3;

    [Fact]
    public async Task A_character_loads_with_its_set_and_last_rolls_newest_first()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        for (var total = 1; total <= 5; total++)
        {
            var roll = CreateRoll(character.Id, "Marcus Varro", total);
            await pg.RunAsync(db => DiceBook.WriteAsync(db, roll, default));
        }

        await pg.RunAsync(db => DiceBook.SaveSetAsync(db, character.Id, "daedric", default));

        var record = await pg.RunAsync(db => DiceBook.LoadAsync(db, character.Id, Recent, default));
        Assert.Equal("daedric", record.Set);
        Assert.Equal([5, 4, 3], record.Recent.Select(roll => roll.Total));
    }

    [Fact]
    public async Task A_character_that_never_rolled_has_no_set_and_no_rolls()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);

        var record = await pg.RunAsync(db => DiceBook.LoadAsync(db, character.Id, Recent, default));

        Assert.Null(record.Set);
        Assert.Empty(record.Recent);
    }

    [Fact]
    public async Task The_log_pages_newest_first_and_filters_by_a_part_of_the_name()
    {
        var first = await Seeds.CreateCharacterAsync(pg.Factory);
        var second = await Seeds.CreateCharacterAsync(pg.Factory);
        var name = $"Lucia {Guid.NewGuid():N}";
        for (var total = 1; total <= 3; total++)
        {
            var mine = CreateRoll(first.Id, name, total);
            var other = CreateRoll(second.Id, "Somebody Else", total);
            await pg.RunAsync(db => DiceBook.WriteAsync(db, mine, default));
            await pg.RunAsync(db => DiceBook.WriteAsync(db, other, default));
        }

        var page = await pg.RunAsync(db => DiceBook.LoadPageAsync(db, name[..12], null, 2, default));
        Assert.True(page.More);
        Assert.Equal([3, 2], page.Rolls.Select(roll => roll.Total));
        Assert.All(page.Rolls, roll => Assert.Equal(name, roll.CharacterName));

        var next = await pg.RunAsync(db => DiceBook.LoadPageAsync(db, name[..12], page.Rolls[^1].Id, 2, default));
        Assert.False(next.More);
        Assert.Equal([1], next.Rolls.Select(roll => roll.Total));
    }

    [Fact]
    public async Task A_name_filter_takes_like_signs_as_they_are()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        var roll = CreateRoll(character.Id, "Marcus Varro", 7);
        await pg.RunAsync(db => DiceBook.WriteAsync(db, roll, default));

        var page = await pg.RunAsync(db => DiceBook.LoadPageAsync(db, "%", null, 10, default));

        Assert.Empty(page.Rolls);
    }

    private static DiceRoll CreateRoll(long characterId, string name, int total) => new()
    {
        At = Seeds.Now,
        CharacterId = characterId,
        CharacterName = name,
        Asked = "1d20",
        Dice = $"20:{total}",
        Total = total,
        Area = "L_ICMarketDistrict",
    };
}
