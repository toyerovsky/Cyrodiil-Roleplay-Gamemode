using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Building;

public static class PlotBook
{
    // The longest key a piece of the building catalogue may have.
    public const int MostPieceLength = ColumnLengths.PieceKey;

    public static Task<List<Plot>> LoadPlotsAsync(CyrodiilContext db, CancellationToken ct) =>
        db.Plots.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);

    public static Task<List<PlotObject>> LoadObjectsAsync(CyrodiilContext db, CancellationToken ct) =>
        db.PlotObjects.AsNoTracking().OrderBy(o => o.Id).ToListAsync(ct);

    public static async Task<long> AddPlotAsync(CyrodiilContext db, Plot plot, CancellationToken ct)
    {
        db.Plots.Add(plot);
        await db.SaveChangesAsync(ct);
        return plot.Id;
    }

    // Its pieces go with it. False when it was gone already.
    public static async Task<bool> RemovePlotAsync(CyrodiilContext db, long plotId, CancellationToken ct) =>
        await db.Plots.Where(p => p.Id == plotId).ExecuteDeleteAsync(ct) > 0;

    public static async Task<long> AddObjectAsync(CyrodiilContext db, PlotObject piece, CancellationToken ct)
    {
        db.PlotObjects.Add(piece);
        await db.SaveChangesAsync(ct);
        return piece.Id;
    }

    // False when it was gone already.
    public static async Task<bool> MoveObjectAsync(CyrodiilContext db, long objectId, float x, float y, float z, float yaw, CancellationToken ct) =>
        await db.PlotObjects.Where(o => o.Id == objectId)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.X, x).SetProperty(o => o.Y, y).SetProperty(o => o.Z, z).SetProperty(o => o.Yaw, yaw), ct) > 0;

    // False when it was gone already.
    public static async Task<bool> RemoveObjectAsync(CyrodiilContext db, long objectId, CancellationToken ct) =>
        await db.PlotObjects.Where(o => o.Id == objectId).ExecuteDeleteAsync(ct) > 0;
}
