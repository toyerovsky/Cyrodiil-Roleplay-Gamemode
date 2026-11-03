using System.Diagnostics;
using CyrodiilRP.Data;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Tests.Database;

// Run as a process, as the deploy runs it.
public sealed class MigratorTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string MigratorDll = "CyrodiilRP.Migrator.dll";
    private const string UpToDate = "The database is up to date.";

    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task An_empty_database_ends_at_the_latest_migration_and_a_second_run_does_nothing()
    {
        var connectionString = await pg.CreateEmptyAsync();

        var first = await RunAsync(connectionString);
        var second = await RunAsync(connectionString);

        Assert.Equal(0, first.ExitCode);
        Assert.Contains(CyrodiilContextFactory.FindLatestMigration(), first.Output, StringComparison.Ordinal);
        Assert.Equal(0, second.ExitCode);
        Assert.Contains(UpToDate, second.Output, StringComparison.Ordinal);
        await using var db = new CyrodiilContextFactory(connectionString).Create();
        Assert.Equal(CyrodiilContextFactory.FindLatestMigration(), (await db.Database.GetAppliedMigrationsAsync()).Last());
    }

    private static async Task<MigratorRun> RunAsync(string connectionString)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, MigratorDll));
        start.ArgumentList.Add(connectionString);

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(Patience);
        return new MigratorRun(process.ExitCode, await output + await errors);
    }

    private sealed record MigratorRun(int ExitCode, string Output);
}
