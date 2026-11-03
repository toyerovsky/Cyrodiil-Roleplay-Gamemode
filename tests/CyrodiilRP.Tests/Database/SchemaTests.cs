using CyrodiilRP.Data;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

public sealed class SchemaTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task The_migrations_match_the_model()
    {
        await using var db = pg.Factory.Create();

        Assert.False(db.Database.HasPendingModelChanges(), "The model changed without a migration: dotnet ef migrations add.");
        Assert.Equal(CyrodiilContextFactory.FindLatestMigration(), (await db.Database.GetAppliedMigrationsAsync()).Last());
    }

    [Fact]
    public async Task The_history_lives_in_its_own_table()
    {
        var tables = await pg.RunAsync(db => db.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync());

        Assert.Contains(CyrodiilContextFactory.HistoryTable, tables);
        Assert.DoesNotContain("__EFMigrationsHistory", tables);
    }

    // Every name of ours is snake case, so hand-written SQL never needs quotes. The history table is EF's.
    [Fact]
    public async Task Every_table_column_index_and_constraint_is_snake_case()
    {
        var names = await pg.RunAsync(db => db.Database.SqlQuery<string>($"""
            SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'public'
            UNION SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name <> {CyrodiilContextFactory.HistoryTable}
            UNION SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename <> {CyrodiilContextFactory.HistoryTable}
            UNION SELECT constraint_name FROM information_schema.table_constraints WHERE table_schema = 'public' AND table_name <> {CyrodiilContextFactory.HistoryTable}
            """).ToListAsync());

        Assert.DoesNotContain(names, name => name.Any(char.IsUpper));
    }
}
