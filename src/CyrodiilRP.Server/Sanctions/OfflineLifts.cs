using CyrodiilRP.Data;
using CyrodiilRP.Data.Conditions;
using CyrodiilRP.Data.Sanctions;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Sanctions;

// The account's own roles are weighed, as for a player online: a player who is away has none loaded.
[Service]
internal sealed partial class OfflineLifts(StaffRoles roles, Persistence persistence, Audit audit)
{
    public Task LiftConditionAsync(CommandCall call, Guid readyMId, string key, string action, Func<string, Phrase> notThere, Func<string, Phrase> lifted) =>
        LiftAsync(call, readyMId, action, (db, ct) => ConditionBook.RemoveForAccountAsync(db, readyMId, key, ct), notThere, lifted);

    // A banned player is never loaded, so a ban is always lifted from an account that is away.
    public Task LiftBanAsync(CommandCall call, Guid readyMId, string action) =>
        LiftAsync(call, readyMId, action, (db, ct) => SanctionBook.UnbanAsync(db, readyMId, ct),
            _ => SanctionsText.NotBanned(readyMId), _ => SanctionsText.Unbanned(readyMId));

    private async Task LiftAsync(CommandCall call, Guid readyMId, string action, Func<CyrodiilContext, CancellationToken, Task<bool>> remove,
        Func<string, Phrase> notThere, Func<string, Phrase> lifted)
    {
        var account = await persistence.Reader.RunAsync((db, ct) => StaffBook.FindAccountAsync(db, readyMId, ct));
        if (!call.Sender.IsLive)
            return;

        if (account is null)
        {
            call.Refuse(StaffText.NoAccount(readyMId));
            return;
        }

        if (!call.Outranks(account.LastNickname, roles.ResolveRoles(readyMId, account.Roles)))
            return;

        var removed = await persistence.Writer.EnqueueAsync(action, remove);
        if (removed)
            audit.Log(action, call.Sender.Ref, OnlinePlayer.FormatRef(readyMId));

        if (call.Sender.IsLive)
            call.Reply(removed ? lifted(account.LastNickname) : notThere(account.LastNickname));
    }
}
