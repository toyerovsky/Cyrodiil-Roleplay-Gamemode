using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Economy;

public static class GoldDeliveryBook
{
    public static Task<List<PendingGold>> LoadPendingAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.GoldDeliveries
            .Where(d => d.CharacterId == characterId && d.DeliveredAt == null)
            .OrderBy(d => d.Id)
            .Select(d => new PendingGold(d.Id, d.Amount))
            .ToListAsync(ct);

    public static async Task OweAsync(CyrodiilContext db, long characterId, int amount, GoldDeliveryReason reason, DateTimeOffset now, CancellationToken ct)
    {
        db.GoldDeliveries.Add(new GoldDelivery { CharacterId = characterId, Amount = amount, Reason = reason, CreatedAt = now });
        await db.SaveChangesAsync(ct);
    }

    // False when it was claimed already, so the same row is never handed over twice.
    public static async Task<bool> ClaimAsync(CyrodiilContext db, long deliveryId, DateTimeOffset now, CancellationToken ct) =>
        await db.GoldDeliveries
            .Where(d => d.Id == deliveryId && d.DeliveredAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.DeliveredAt, now), ct) > 0;

    // Owed again: the claim went through but the gold could not be given.
    public static Task ReleaseAsync(CyrodiilContext db, long deliveryId, CancellationToken ct) =>
        db.GoldDeliveries
            .Where(d => d.Id == deliveryId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.DeliveredAt, (DateTimeOffset?)null), ct);
}
