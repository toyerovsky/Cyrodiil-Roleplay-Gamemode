using System.Globalization;
using CyrodiilRP.Common.Chat;
using CyrodiilRP.Data.Sanctions;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Conditions;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using ReadyM.Relay.Server.Sdk.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Sanctions;

[Service]
internal sealed partial class SanctionCommands(
    CommandRegistry commands,
    CharacterConditions conditions,
    OfflineLifts lifts,
    OnlinePlayers online,
    IPlayers players,
    StaffRoles roles,
    Persistence persistence,
    RpChat chat,
    Audit audit,
    InteractionMenu interactions,
    Timers timers,
    Fire fire)
{
    private const string KickOption = "player.kick";
    private const int KickOrder = 130;

    private const string KickAction = "admin.kick";
    private const string BanAction = "admin.ban";
    private const string UnbanAction = "admin.unban";
    private const string WarnAction = "admin.warn";
    private const string DateFormat = "yyyy-MM-dd HH:mm 'UTC'";

    // The platform's kick carries no reason, so the reason is said first and the kick follows once it has arrived.
    private static readonly TimeSpan KickAfterNotice = TimeSpan.FromSeconds(2);

    // A player who just joined has no pages up for the first seconds.
    private static readonly TimeSpan BannedNoticeDelay = TimeSpan.FromSeconds(4);

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Kick, SanctionsText.CmdKick, Kick);
        commands.AddStaff(StaffPermissions.Ban, SanctionsText.CmdBan, Ban);
        commands.AddStaff(StaffPermissions.Unban, SanctionsText.CmdUnban, Unban);
        commands.AddStaff(StaffPermissions.Warn, SanctionsText.CmdWarn, Warn);
        commands.AddStaff(StaffPermissions.Why, SanctionsText.CmdWhy, Explain, offDuty: true);
        online.Banned += OnBanned;

        // A sanction needs a reason, so the wheel starts the command and the staff member writes it.
        interactions.Add(new InteractionOption
        {
            Id = KickOption,
            Label = SanctionsText.OptionKick,
            Icon = WheelIcons.Door,
            Order = KickOrder,
            Permission = StaffPermissions.Kick,
            Run = (actor, target) => chat.Prefill(actor, $"/{StaffPermissions.Kick} {target.Number} "),
        });
    }

    private void OnBanned(OnlinePlayer player) =>
        timers.RunAfter("sanctions.ban_notice", BannedNoticeDelay, () => KickWithNotice(player, FormatBanNotice(player.BannedUntil!.Value, player.BanReason ?? "")));

    private void Kick(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target) || !call.TryReason(1, out var reason))
            return;

        audit.Log(KickAction, call.Sender.Ref, target.Ref, new ReasonDetails(reason));
        KickWithNotice(target, SanctionsText.KickedNotice(reason));
        call.Reply(SanctionsText.Kicked(target.Name));
    }

    private void Ban(CommandCall call)
    {
        if (!call.TryAccount(0, out var target) || !call.TryMinutes(1, out var duration) || !call.TryReason(2, out var reason))
            return;

        if (target.ReadyMId == call.Sender.ReadyMId)
        {
            call.Refuse(CommandsText.NotYourself);
            return;
        }

        fire.Run(BanAction, () => BanAsync(call, target, duration, reason));
    }

    // The account's own roles, never an online player's that may not be loaded yet.
    private async Task BanAsync(CommandCall call, AccountTarget target, TimeSpan? duration, string reason)
    {
        var account = await persistence.Reader.RunAsync((db, ct) => StaffBook.FindAccountAsync(db, target.ReadyMId, ct));
        if (!call.Sender.IsLive)
            return;

        if (account is null)
        {
            call.Refuse(StaffText.NoAccount(target.ReadyMId));
            return;
        }

        var name = target.Online?.OocName ?? account.LastNickname;
        if (!call.Outranks(name, roles.ResolveRoles(target.ReadyMId, account.Roles)))
            return;

        var until = duration is { } length ? DateTimeOffset.UtcNow + length : SanctionBook.Forever;
        await persistence.Writer.EnqueueAsync(BanAction, (db, ct) => SanctionBook.BanAsync(db, target.ReadyMId, until, reason, ct));

        audit.Log(BanAction, call.Sender.Ref, OnlinePlayer.FormatRef(target.ReadyMId), new SanctionDetails((int?)duration?.TotalMinutes, reason));

        // Whoever holds the account now, which may be a session that joined after the ban was asked for.
        if (online.TryFind(target.ReadyMId, out var player))
            KickWithNotice(player, FormatBanNotice(until, reason));

        if (call.Sender.IsLive)
            call.Reply(duration is null ? SanctionsText.BannedForever(name) : SanctionsText.Banned(name, FormatDate(until)));
    }

    private void Unban(CommandCall call)
    {
        if (call.Words.Count == 0 || !Guid.TryParse(call.Words[0], out var readyMId))
        {
            call.Refuse(SanctionsText.UnbanTarget);
            return;
        }

        fire.Run(UnbanAction, () => lifts.LiftBanAsync(call, readyMId, UnbanAction));
    }

    // Holds nothing back: the warning stays in the player's history, which the staff read in the panel.
    private void Warn(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target) || !call.TryReason(1, out var reason))
            return;

        audit.Log(WarnAction, call.Sender.Ref, target.Ref, new ReasonDetails(reason));
        chat.SendTo(target, ChatLineKind.Announcement, SanctionsText.WarnedNotice(reason));
        chat.Notify(target, SanctionsText.WarnedNotice(reason));
        call.Reply(SanctionsText.Warned(target.Name));
    }

    private void Explain(CommandCall call)
    {
        if (!call.TryTarget(0, out var target))
            return;

        var silencing = conditions.ListSilencing(target).Select(c => c.Key).ToList();
        call.Reply(silencing.Count == 0
            ? SanctionsText.WhyAllowed(target.Name)
            : SanctionsText.WhyDenied(target.Name, string.Join(", ", silencing)));
    }

    private void KickWithNotice(OnlinePlayer player, Phrase notice)
    {
        if (!player.IsLive)
            return;

        chat.SendTo(player, ChatLineKind.Announcement, notice);
        chat.Notify(player, notice);
        timers.RunAfter("sanctions.kick", KickAfterNotice, () =>
        {
            if (player.IsLive)
                players.Kick(player.Id);
        });
    }

    private static Phrase FormatBanNotice(DateTimeOffset until, string reason) =>
        until >= SanctionBook.Forever ? SanctionsText.BannedForeverNotice(reason) : SanctionsText.BannedNotice(FormatDate(until), reason);

    private static string FormatDate(DateTimeOffset at) => at.ToString(DateFormat, CultureInfo.InvariantCulture);

    private sealed record ReasonDetails(string Reason);

    private sealed record SanctionDetails(int? Minutes, string Reason);
}
