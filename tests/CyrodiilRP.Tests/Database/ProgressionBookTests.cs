using CyrodiilRP.Data;
using CyrodiilRP.Data.Progression;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class ProgressionBookTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Progression_soft_cap_and_skill_peaks_survive_a_new_context_and_a_later_write()
    {
        var character = await Seeds.CreateCharacterAsync(pg.Factory);
        Assert.Null(await pg.RunAsync(db => ProgressionBook.FindAsync(db, character.Id, CancellationToken.None)));
        await pg.RunAsync(db => ProgressionBook.SaveAsync(db, Row(character.Id, 2), CancellationToken.None));
        await pg.RunAsync(db => ProgressionBook.SaveAsync(db, Row(character.Id, 3), CancellationToken.None));
        var read = Assert.IsType<CharacterProgression>(await pg.RunAsync(db => ProgressionBook.FindAsync(db, character.Id, CancellationToken.None)));
        Assert.Equal(3, read.Level);
        Assert.Equal(25, read.Xp);
        Assert.Equal(2, read.PerkPoints);
        Assert.Equal(Seeds.Now, read.ReachedAt);
        Assert.Equal(Seeds.Now, read.GatheringSince);
        Assert.Equal(1500, read.GatheringXp);
        Assert.Equal([30, 40], read.SkillPeaks);
        Assert.Equal(1, await pg.RunAsync(db => db.CharacterProgression.CountAsync(x => x.CharacterId == character.Id)));
        await pg.RunAsync(db => db.Characters.Where(x => x.Id == character.Id).ExecuteDeleteAsync());
        Assert.Null(await pg.RunAsync(db => ProgressionBook.FindAsync(db, character.Id, CancellationToken.None)));
    }

    private static CharacterProgression Row(long characterId, int level) => new()
    {
        CharacterId = characterId, Level = level, Xp = 25, PerkPoints = 2,
        ReachedAt = Seeds.Now, GatheringSince = Seeds.Now, GatheringXp = 1500, SkillPeaks = [30, 40],
    };
}
