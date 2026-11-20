using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Guides;

public static class GuideBook
{
    public static Task<List<GuideTopic>> ReadSeenAsync(CyrodiilContext db, long accountId, CancellationToken ct) =>
        db.AccountGuides.AsNoTracking().Where(x => x.AccountId == accountId).Select(x => x.Topic).ToListAsync(ct);

    // A guide seen twice keeps the first time.
    public static async Task MarkSeenAsync(CyrodiilContext db, long accountId, GuideTopic topic, DateTimeOffset at, CancellationToken ct)
    {
        if (await db.AccountGuides.AnyAsync(x => x.AccountId == accountId && x.Topic == topic, ct))
            return;

        db.AccountGuides.Add(new AccountGuide { AccountId = accountId, Topic = topic, SeenAt = at });
        await db.SaveChangesAsync(ct);
    }
}
