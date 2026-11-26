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

    // The piece goes and its parts come in one transaction: false, and nothing written, when it was gone already. The parts
    // get their ids.
    public static async Task<bool> ReplaceObjectAsync(CyrodiilContext db, long objectId, IReadOnlyList<PlotObject> parts, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.PlotObjects.Where(o => o.Id == objectId).ExecuteDeleteAsync(ct) == 0)
            return false;

        db.PlotObjects.AddRange(parts);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    // The plot written to the character while it is for sale: false when another owns it or it is gone.
    public static async Task<bool> BuyAsync(CyrodiilContext db, long plotId, long characterId, CancellationToken ct) =>
        await db.Plots.Where(p => p.Id == plotId && p.OwnerCharacterId == null)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.OwnerCharacterId, characterId), ct) > 0;

    // False when it was gone already.
    public static async Task<bool> MoveSignAsync(CyrodiilContext db, long plotId, float x, float y, float z, float yaw, CancellationToken ct) =>
        await db.Plots.Where(p => p.Id == plotId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.SignX, x).SetProperty(p => p.SignY, y).SetProperty(p => p.SignZ, z)
                .SetProperty(p => p.SignYaw, yaw), ct) > 0;

    // False when it was gone already.
    public static async Task<bool> RemoveObjectAsync(CyrodiilContext db, long objectId, CancellationToken ct) =>
        await db.PlotObjects.Where(o => o.Id == objectId).ExecuteDeleteAsync(ct) > 0;
}
