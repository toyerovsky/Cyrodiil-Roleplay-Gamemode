using CyrodiilRP.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Infrastructure;

[Service]
internal sealed partial class Persistence
{
    private readonly string _connectionString;

    public Persistence(RpConfig config, ILogger logger)
    {
        _connectionString = config.Database.ConnectionString;
        Factory = new CyrodiilContextFactory(_connectionString);
        Writer = new DatabaseWriter(Factory, logger);
        Reader = new DatabaseReader(Factory);
    }

    public CyrodiilContextFactory Factory { get; }

    public DatabaseWriter Writer { get; }

    public DatabaseReader Reader { get; }

    // The server never migrates: it refuses a database at any other migration than its own build's.
    public string RequireSchema()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("CyrodiilRP has no database.connectionString in server/config.json.");

        var expected = CyrodiilContextFactory.FindLatestMigration();
        string? actual;
        try
        {
            using var db = Factory.Create();
            actual = db.Database.GetAppliedMigrations().LastOrDefault();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"CyrodiilRP cannot read the database schema ({exception.Message}). Is the database up?", exception);
        }

        if (actual != expected)
            throw new InvalidOperationException(
                $"CyrodiilRP needs the database at migration '{expected}', it is at '{actual ?? "none"}'. Run CyrodiilRP.Migrator.");

        return expected;
    }
}
