using CyrodiilRP.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Run before starting the server after every update: the server refuses a database at another migration than its own.
var connectionString = args.FirstOrDefault()
                       ?? Environment.GetEnvironmentVariable(CyrodiilContextFactory.ConnectionVariable)
                       ?? CyrodiilContextFactory.LocalConnectionString;

var target = new NpgsqlConnectionStringBuilder(connectionString);
Console.WriteLine($"Database {target.Database} on {target.Host}:{target.Port}.");

await using var db = new CyrodiilContextFactory(connectionString).Create();

var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count == 0)
{
    Console.WriteLine("The database is up to date.");
    return 0;
}

Console.WriteLine($"Applying {pending.Count} migration(s): {string.Join(", ", pending)}");
await db.Database.MigrateAsync();
Console.WriteLine("Done.");
return 0;
