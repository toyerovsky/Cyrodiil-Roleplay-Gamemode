using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using CyrodiilRP.Common.Building;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Building;
using CyrodiilRP.Server.Infrastructure;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Building;

// The plots and the pieces on them: every change is written first and kept in memory after, so a restart reads back
// what players saw. What shows them (the props in the world, architect mode) follows AreaChanged. A database that is not
// up yet only keeps the plots away until it is.
[Service]
internal sealed partial class Plots(
    Persistence persistence,
    RpConfig config,
    Callbacks callbacks,
    Fire fire,
    Timers timers,
    SystemClock clock,
    ILogger logger)
{
    private const string LoadJob = "plots.load";
    private const string AddPlotJob = "plots.add_plot";
    private const string RemovePlotJob = "plots.remove_plot";
    private const string AddObjectJob = "plots.add_object";
    private const string MoveObjectJob = "plots.move_object";
    private const string RemoveObjectJob = "plots.remove_object";
    private const string AreaChangedEvent = "plots.area_changed";
    private const string PlotRemovedEvent = "plots.plot_removed";

    private readonly PlotRegistry _plots = new();
    private readonly PlotObjectRegistry _objects = new();
    private PieceRegistry? _pieces;

    // The area whose plots or pieces changed, or were read at start.
    public event Action<AreaId>? AreaChanged;

    public event Action<PlotRecord>? PlotRemoved;

    public BuildingSettings Settings => config.Building;

    public PieceRegistry Pieces => _pieces ??= new PieceRegistry(config.Building.Pieces);

    public IReadOnlyCollection<PlotRecord> All => _plots.All;

    public bool TryFindPlot(long id, [NotNullWhen(true)] out PlotRecord? plot) => _plots.TryFind(id, out plot);

    public bool TryFindObject(long id, [NotNullWhen(true)] out PlotObjectRecord? piece) => _objects.TryFind(id, out piece);

    public IReadOnlyList<PlotRecord> ListIn(AreaId area) => _plots.ListIn(area);

    public IReadOnlyList<PlotObjectRecord> ListOn(long plotId) => _objects.ListOn(plotId);

    public int CountOn(long plotId) => _objects.CountOn(plotId);

    public PlotRecord? FindAt(AreaId area, Vector3 position) => _plots.FindAt(area, position.X, position.Y);

    public bool Overlaps(AreaId area, PlotBox box) => _plots.Overlaps(area, box);

    private void Start() => Load();

    private void Load() => fire.Run(LoadJob, LoadAsync);

    private async Task LoadAsync()
    {
        List<Plot> plots;
        List<PlotObject> objects;
        try
        {
            plots = await persistence.Reader.RunAsync(PlotBook.LoadPlotsAsync);
            objects = await persistence.Reader.RunAsync(PlotBook.LoadObjectsAsync);
        }
        catch (Exception exception) when (DatabaseFailures.Is(exception))
        {
            logger.LogWarning("[CyrodiilRP] The plots could not be read ({Problem}), trying again in {Seconds} s.",
                exception.GetBaseException().Message, BuildingRules.LoadRetry.TotalSeconds);
            timers.RunAfter(LoadJob, BuildingRules.LoadRetry, Load);
            return;
        }

        _plots.Load(plots.Select(ToRecord));
        _objects.Load(objects.Select(ToRecord), id => _plots.TryFind(id, out _));

        // Players who came while the database was not up see the plots of their area now.
        foreach (var area in plots.Select(p => new AreaId(p.Area)).Distinct())
            callbacks.Raise(AreaChangedEvent, AreaChanged, area);
    }

    public async Task<PlotRecord> AddPlotAsync(string area, PlotBox box, PlotKind kind, int price, int weeklyTax, Vector3 sign, float signYaw, string madeByRef)
    {
        var row = new Plot
        {
            Area = area,
            FirstTileX = box.FirstTileX,
            FirstTileY = box.FirstTileY,
            Width = box.Width,
            Depth = box.Depth,
            GroundZ = box.GroundZ,
            Height = box.Height,
            Kind = kind,
            Price = price,
            WeeklyTax = weeklyTax,
            SignX = sign.X,
            SignY = sign.Y,
            SignZ = sign.Z,
            SignYaw = signYaw,
            MadeByRef = madeByRef,
            MadeAt = clock.UtcNow,
        };
        var id = await persistence.Writer.EnqueueAsync(AddPlotJob, (db, ct) => PlotBook.AddPlotAsync(db, row, ct));
        var plot = ToRecord(row) with { Id = id };
        _plots.Add(plot);
        callbacks.Raise(AreaChangedEvent, AreaChanged, plot.AreaId);
        return plot;
    }

    // Out of memory only once it is out of the database, its pieces with it. False when another removal took it first.
    public async Task<bool> RemovePlotAsync(PlotRecord plot)
    {
        var removed = await persistence.Writer.EnqueueAsync(RemovePlotJob, (db, ct) => PlotBook.RemovePlotAsync(db, plot.Id, ct));
        _plots.Remove(plot.Id);
        _objects.RemovePlot(plot.Id);
        callbacks.Raise(AreaChangedEvent, AreaChanged, plot.AreaId);
        callbacks.Raise(PlotRemovedEvent, PlotRemoved, plot);
        return removed;
    }

    public async Task<PlotObjectRecord> AddObjectAsync(PlotRecord plot, Piece piece, Vector3 position, float yaw, PlotObjectState state, uint refId,
        string placedByRef)
    {
        var row = new PlotObject
        {
            PlotId = plot.Id,
            Piece = piece.Key,
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            Yaw = yaw,
            State = state,
            RefId = refId == 0 ? null : refId,
            PlacedByRef = placedByRef,
            PlacedAt = clock.UtcNow,
        };
        var id = await persistence.Writer.EnqueueAsync(AddObjectJob, (db, ct) => PlotBook.AddObjectAsync(db, row, ct));
        var placed = ToRecord(row) with { Id = id };
        _objects.Put(placed);
        callbacks.Raise(AreaChangedEvent, AreaChanged, plot.AreaId);
        return placed;
    }

    // Null when it was gone already.
    public async Task<PlotObjectRecord?> MoveObjectAsync(PlotRecord plot, PlotObjectRecord piece, Vector3 position, float yaw)
    {
        var moved = await persistence.Writer.EnqueueAsync(MoveObjectJob,
            (db, ct) => PlotBook.MoveObjectAsync(db, piece.Id, position.X, position.Y, position.Z, yaw, ct));
        if (!moved || !_objects.TryFind(piece.Id, out var current))
            return null;

        var placed = current with { Position = position, Rotation = current.Rotation with { Y = yaw } };
        _objects.Put(placed);
        callbacks.Raise(AreaChangedEvent, AreaChanged, plot.AreaId);
        return placed;
    }

    // False when it was gone already.
    public async Task<bool> RemoveObjectAsync(PlotRecord plot, PlotObjectRecord piece)
    {
        var removed = await persistence.Writer.EnqueueAsync(RemoveObjectJob, (db, ct) => PlotBook.RemoveObjectAsync(db, piece.Id, ct));
        _objects.Remove(piece.Id);
        callbacks.Raise(AreaChangedEvent, AreaChanged, plot.AreaId);
        return removed;
    }

    private static PlotRecord ToRecord(Plot row) => new(
        row.Id,
        row.Area,
        new PlotBox(row.FirstTileX, row.FirstTileY, row.Width, row.Depth, row.GroundZ, row.Height),
        row.Kind,
        row.Price,
        row.WeeklyTax,
        row.OwnerCharacterId,
        new Vector3(row.SignX, row.SignY, row.SignZ),
        row.SignYaw);

    private static PlotObjectRecord ToRecord(PlotObject row) =>
        new(row.Id, row.PlotId, row.Piece, new Vector3(row.X, row.Y, row.Z), new Vector3(row.Pitch, row.Yaw, row.Roll), row.State,
            row.RefId is { } refId ? (uint)refId : 0);
}
