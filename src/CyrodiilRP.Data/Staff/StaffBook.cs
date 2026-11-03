using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Staff;

public static class StaffBook
{
    public static Task<List<StaffRole>> LoadRolesAsync(CyrodiilContext db, CancellationToken ct) =>
        db.StaffRoles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);

    public static Task<AccountSummary?> FindAccountAsync(CyrodiilContext db, Guid readyMId, CancellationToken ct) =>
        db.Accounts
            .Where(a => a.ReadyMId == readyMId)
            .Select(a => new AccountSummary(a.Id, a.ReadyMId, a.LastNickname, a.Roles, a.BannedUntil))
            .SingleOrDefaultAsync(ct);

    public static Task SetRolesAsync(CyrodiilContext db, Guid readyMId, string[] roles, CancellationToken ct) =>
        db.Accounts
            .Where(a => a.ReadyMId == readyMId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Roles, roles), ct);
}
