using System.Numerics;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Traders;
using CyrodiilRP.Server.Infrastructure;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Traders;

// A database that is not up yet only keeps the traders away until it is.
[Service]
internal sealed partial class TraderSpots(Persistence persistence, Fire fire, Timers timers, ILogger logger)
{
    private const string LoadJob = "traders.load";
    private const string AddJob = "traders.add";
    private const string RemoveJob = "traders.remove";
    private const string NpcJob = "traders.npc";

    private readonly List<TraderSpotRecord> _spots = [];

    public IReadOnlyList<TraderSpotRecord> All => _spots;

    private void Start() => Load();

    private void Load() => fire.Run(LoadJob, LoadAsync);

    private async Task LoadAsync()
    {
        List<TraderSpot> rows;
        try
        {
            rows = await persistence.Reader.RunAsync(TraderSpotBook.LoadAllAsync);
        }
        catch (Exception exception) when (DatabaseFailures.Is(exception))
        {
            logger.LogWarning("[CyrodiilRP] The traders' spots could not be read ({Problem}), trying again in {Seconds} s.",
                exception.GetBaseException().Message, TraderRules.LoadRetry.TotalSeconds);
            timers.RunAfter(LoadJob, TraderRules.LoadRetry, Load);
            return;
        }

        // A spot placed while the read was on its way is in both.
        foreach (var row in rows.Where(row => _spots.All(spot => spot.Id != row.Id)))
            _spots.Add(new TraderSpotRecord(row.Id, row.Shop, row.Area, row.Cell, new Vector3(row.X, row.Y, row.Z), row.Yaw) { NpcRefId = (uint)row.NpcRefId });
    }

    public async Task<TraderSpotRecord> AddAsync(string shop, string area, string cell, Vector3 position, float yaw, string placedByRef)
    {
        var row = new TraderSpot
        {
            Shop = shop,
            Area = area,
            Cell = cell,
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            Yaw = yaw,
            PlacedByRef = placedByRef,
            PlacedAt = DateTimeOffset.UtcNow,
        };
        var id = await persistence.Writer.EnqueueAsync(AddJob, (db, ct) => TraderSpotBook.AddAsync(db, row, ct));
        var spot = new TraderSpotRecord(id, shop, area, cell, position, yaw);
        _spots.Add(spot);
        return spot;
    }

    // Out of memory only once it is out of the database. False when another removal took it first.
    public async Task<bool> RemoveAsync(TraderSpotRecord spot)
    {
        var removed = await persistence.Writer.EnqueueAsync(RemoveJob, (db, ct) => TraderSpotBook.RemoveAsync(db, spot.Id, ct));
        _spots.Remove(spot);
        return removed;
    }

    public void SetNpc(TraderSpotRecord spot, uint npcRefId)
    {
        spot.NpcRefId = npcRefId;
        fire.Run(NpcJob, () => persistence.Writer.EnqueueAsync(NpcJob, (db, ct) => TraderSpotBook.SetNpcAsync(db, spot.Id, npcRefId, ct)));
    }
}
