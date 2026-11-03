using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CyrodiilRP.Data;

public sealed class CyrodiilContextFactory(string connectionString)
{
    // The development database of infra/docker-compose.yml.
    public const string LocalConnectionString = "Host=localhost;Port=5433;Database=cyrodiilrp;Username=cyrodiilrp;Password=cyrodiilrp";

    // Where the migrator and the EF tools look first.
    public const string ConnectionVariable = "CYRODIILRP_DB";

    internal const string HistoryTable = "__ef_migrations";

    private readonly DbContextOptions<CyrodiilContext> _options = BuildOptions(connectionString);

    public CyrodiilContext Create() => new(_options);

    public static string FindLatestMigration()
    {
        using var db = new CyrodiilContext(BuildOptions("Host=unused"));
        return db.Database.GetMigrations().Last();
    }

    public static bool IsUniqueViolation(Exception exception) =>
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
            or PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static DbContextOptions<CyrodiilContext> BuildOptions(string connectionString) =>
        new DbContextOptionsBuilder<CyrodiilContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(HistoryTable))
            .Options;
}
