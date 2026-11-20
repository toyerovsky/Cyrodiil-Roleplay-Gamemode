using System.Numerics;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Gambling;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Gambling;

[Service]
internal sealed partial class GamblingTables(Persistence persistence, Proximity proximity, RpConfig config, SystemClock clock)
{
    private readonly GamblingTableRegistry _registry = new();
    public bool Ready { get; private set; }
    public IReadOnlyCollection<GamblingTableRecord> All => _registry.Tables;

    public async Task LoadAsync()
    {
        var tables = await persistence.Reader.RunAsync(GamblingBook.LoadTablesAsync);
        foreach (var table in tables)
            _registry.Add(new GamblingTableRecord(table.Id, table.Area, table.Cell, new Vector3(table.X, table.Y, table.Z), table.Yaw));

        Ready = true;
    }

    public bool IsAt(OnlinePlayer player, GamblingTableRecord table) => _registry.Contains(table.Id) && player.IsLive && player.IsLoaded && player.IsPlaying
        && proximity.MeasureDistance(player.Id, table.AreaId, table.Position) is { } distance && distance <= config.Gambling.TableReach;

    public GamblingTableRecord? Find(OnlinePlayer player) => All.Where(table => IsAt(player, table))
        .OrderBy(table => proximity.MeasureDistance(player.Id, table.AreaId, table.Position)).ThenBy(table => table.Id).FirstOrDefault();

    // The nearest to the first player, as Find gives them: the table their window shows is the table of the game.
    public GamblingTableRecord? Shared(OnlinePlayer first, OnlinePlayer second) =>
        All.Where(table => IsAt(first, table) && IsAt(second, table))
            .OrderBy(table => proximity.MeasureDistance(first.Id, table.AreaId, table.Position)).ThenBy(table => table.Id).FirstOrDefault();

    public async Task<GamblingTableRecord> PlaceAsync(string area, string cell, Vector3 position, float yaw, string actor)
    {
        var row = new GamblingTable { Area = area, Cell = cell, X = position.X, Y = position.Y, Z = position.Z, Yaw = yaw,
            PlacedAt = clock.UtcNow, PlacedByRef = actor };
        var id = await persistence.Writer.EnqueueAsync("table.place", (db, ct) => GamblingBook.PlaceAsync(db, row, ct));
        var table = new GamblingTableRecord(id, area, cell, position, yaw);
        _registry.Add(table);
        return table;
    }

    public async Task<bool> RemoveAsync(GamblingTableRecord table)
    {
        _registry.Remove(table.Id);
        try
        {
            return await persistence.Writer.EnqueueAsync("table.remove", (db, ct) => GamblingBook.RemoveAsync(db, table.Id, clock.UtcNow, ct));
        }
        catch
        {
            _registry.Add(table);
            throw;
        }
    }
}
