using CyrodiilRP.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CyrodiilRP.Tests.Database;

// A database per test class, cloned from a template migrated once, on one PostgreSQL in Docker for the whole run.
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "cyrodiilrp_template";
    private const string DatabasePrefix = "cyrodiilrp_";

    // Every class running at once holds its own pool, together past PostgreSQL's default of 100 connections.
    private const string MaxConnections = "max_connections=500";

    private static readonly PostgreSqlContainer Container = new PostgreSqlBuilder("postgres:18").WithCommand("-c", MaxConnections).Build();
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _templateReady;

    public CyrodiilContextFactory Factory { get; private set; } = null!;

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        ConnectionString = await CreateAsync($"TEMPLATE \"{TemplateDatabase}\"");
        Factory = new CyrodiilContextFactory(ConnectionString);
    }

    // A database with nothing in it, as before the migrator ever ran.
    public Task<string> CreateEmptyAsync() => CreateAsync("");

    // A context of its own for each step, as the writer gives every job: a context used twice hands back what it tracks.
    public async Task<T> RunAsync<T>(Func<CyrodiilContext, Task<T>> work)
    {
        await using var db = Factory.Create();
        return await work(db);
    }

    public async Task RunAsync(Func<CyrodiilContext, Task> work)
    {
        await using var db = Factory.Create();
        await work(db);
    }

    // Closes the class's pooled connections, to stay within max_connections.
    public Task DisposeAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        NpgsqlConnection.ClearPool(connection);
        return Task.CompletedTask;
    }

    private static async Task<string> CreateAsync(string from)
    {
        var name = DatabasePrefix + Guid.NewGuid().ToString("N");
        await Gate.WaitAsync();
        try
        {
            if (!_templateReady)
            {
                await Container.StartAsync();
                await MigrateTemplateAsync();
                _templateReady = true;
            }

            await ExecuteAsync($"CREATE DATABASE \"{name}\" {from}");
        }
        finally
        {
            Gate.Release();
        }

        return BuildConnectionString(name);
    }

    // Dropped first, so a migration that failed for one class fails the next on its own error, not on "already exists".
    // Its connection is not pooled, so nothing holds the template when the clones are made.
    private static async Task MigrateTemplateAsync()
    {
        await ExecuteAsync($"DROP DATABASE IF EXISTS \"{TemplateDatabase}\"");
        await ExecuteAsync($"CREATE DATABASE \"{TemplateDatabase}\"");

        var builder = new NpgsqlConnectionStringBuilder(BuildConnectionString(TemplateDatabase)) { Pooling = false };
        await using var db = new CyrodiilContextFactory(builder.ConnectionString).Create();
        await db.Database.MigrateAsync();
    }

    private static async Task ExecuteAsync(string sql)
    {
        await using var admin = new NpgsqlConnection(Container.GetConnectionString());
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand(sql, admin);
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(Container.GetConnectionString()) { Database = database }.ConnectionString;
}
