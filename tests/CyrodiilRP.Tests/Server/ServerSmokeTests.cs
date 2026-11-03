using CyrodiilRP.Data;
using CyrodiilRP.Tests.Database;

namespace CyrodiilRP.Tests.Server;

[Trait("Category", "Server")]
public sealed class ServerSmokeTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private const string Up = "[CyrodiilRP] Server half up";
    private const string Listening = "Running server on port";
    private const string NotStarted = "Failed to start the mod in CyrodiilRP.Server";
    private const string RunMigrator = "Run CyrodiilRP.Migrator";
    private const string AssemblyRefused = "Failed to load assembly";

    private static readonly string[] ErrorLevels = ["[ERR]", "[FTL]"];
    private static readonly TimeSpan Boot = TimeSpan.FromSeconds(90);

    // Long enough for the timers of every second (the audit, the expiry check) to have run.
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task The_gamemode_starts_on_a_migrated_database_without_an_error()
    {
        await using var server = await RunningServer.StartAsync(pg.ConnectionString);

        Assert.True(await server.WaitForAsync(Up, Boot), $"No '{Up}':{Environment.NewLine}{server.Output}");
        Assert.True(await server.WaitForAsync(Listening, Boot), $"No '{Listening}':{Environment.NewLine}{server.Output}");
        await Task.Delay(Settle);

        Assert.Contains(server.Lines, line => line.Contains(Up, StringComparison.Ordinal) && line.Contains(CyrodiilContextFactory.FindLatestMigration(), StringComparison.Ordinal));
        Assert.DoesNotContain(server.Lines, line => ErrorLevels.Any(level => line.Contains(level, StringComparison.Ordinal)));
        Assert.DoesNotContain(server.Lines, line => line.Contains(AssemblyRefused, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_gamemode_refuses_a_database_the_migrator_has_not_prepared()
    {
        await using var server = await RunningServer.StartAsync(await pg.CreateEmptyAsync());

        Assert.True(await server.WaitForAsync(NotStarted, Boot), $"No '{NotStarted}':{Environment.NewLine}{server.Output}");
        Assert.True(await server.WaitForAsync(RunMigrator, Boot), $"No '{RunMigrator}':{Environment.NewLine}{server.Output}");

        Assert.DoesNotContain(server.Lines, line => line.Contains(Up, StringComparison.Ordinal));
    }
}
