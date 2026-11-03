using CyrodiilRP.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CyrodiilRP.Tests.Database;

// A database from before characters were written whole, with one left half made, migrated to the latest.
public sealed class UnfinishedCharacterMigrationTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string BeforeWholeCharacters = "20261023141057_Traders";
    private const int Gold = 50;

    private static readonly DateTimeOffset Now = Seeds.Now;

    [Fact]
    public async Task A_half_made_character_goes_with_its_conditions_and_gold_and_a_whole_one_stays()
    {
        var connectionString = await pg.CreateEmptyAsync();
        await using (var before = new CyrodiilContextFactory(connectionString).Create())
            await before.GetService<IMigrator>().MigrateAsync(BeforeWholeCharacters);

        await using var db = new CyrodiilContextFactory(connectionString).Create();
        var whole = await InsertCharacterAsync(db, "Marius", "Varro", done: true);
        await InsertCharacterAsync(db, "John", "Doe", done: false);

        await db.Database.MigrateAsync();

        Assert.Equal([whole], await db.Database.SqlQuery<long>($"SELECT id AS \"Value\" FROM characters").ToListAsync());
        Assert.Equal([whole], await db.Database.SqlQuery<long>($"SELECT character_id AS \"Value\" FROM gold_deliveries").ToListAsync());
        Assert.Equal([whole], await db.Database.SqlQuery<long>($"SELECT character_id AS \"Value\" FROM character_conditions").ToListAsync());
        Assert.Empty(await db.Database.SqlQuery<string>($"""
            SELECT column_name AS "Value" FROM information_schema.columns WHERE table_name = 'characters' AND column_name = 'appearance_done'
            """).ToListAsync());
    }

    // Written as the schema before this migration had it, so with the column the model no longer knows.
    private static async Task<long> InsertCharacterAsync(CyrodiilContext db, string firstName, string lastName, bool done)
    {
        var account = (await db.Database.SqlQuery<long>($"""
            INSERT INTO accounts (readym_id, locale, last_nickname, roles, created_at, last_seen_at)
            VALUES ({Guid.NewGuid()}, 'en', '', {Array.Empty<string>()}, {Now}, {Now}) RETURNING id AS "Value"
            """).ToListAsync()).Single();

        var key = Character.MakeNameKey(firstName, lastName);
        var character = (await db.Database.SqlQuery<long>($"""
            INSERT INTO characters (account_id, first_name, last_name, name_key, appearance_done, created_at, last_played_at, played_seconds)
            VALUES ({account}, {firstName}, {lastName}, {key}, {done}, {Now}, {Now}, 0) RETURNING id AS "Value"
            """).ToListAsync()).Single();

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO gold_deliveries (character_id, amount, reason, created_at) VALUES ({character}, {Gold}, 'StartingGold', {Now})
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO character_conditions (character_id, key, applied_at) VALUES ({character}, 'admin.mute', {Now})
            """);
        return character;
    }
}
