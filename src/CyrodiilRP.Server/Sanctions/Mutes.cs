using CyrodiilRP.Common.Chat;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Conditions;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Sanctions;

[Service]
internal sealed partial class Mutes(
    CommandRegistry commands,
    CharacterConditions conditions,
    OfflineLifts lifts,
    RpChat chat,
    Audit audit,
    InteractionMenu interactions,
    Fire fire)
{
    // Stored with the character, so never renamed.
    public const string MuteKey = "admin.mute";

    private const string MuteOption = "player.mute";
    private const int MuteOrder = 120;

    private const string MuteAction = "admin.mute";
    private const string UnmuteAction = "admin.unmute";

    private readonly ConditionDefinition _mute = conditions.Define(MuteKey, SanctionsText.ConditionMute, FormatRefusal);

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Mute, SanctionsText.CmdMute, Mute);
        commands.AddStaff(StaffPermissions.Unmute, SanctionsText.CmdUnmute, Unmute);

        // A sanction needs a reason, so the wheel starts the command and the staff member writes it.
        interactions.Add(new InteractionOption
        {
            Id = MuteOption,
            Label = SanctionsText.OptionMute,
            Icon = WheelIcons.Mute,
            Order = MuteOrder,
            Permission = StaffPermissions.Mute,
            Run = (actor, target) => chat.Prefill(actor, $"/{StaffPermissions.Mute} {target.Number} "),
        });
    }

    private void Mute(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target) || !call.TryMinutes(1, out var duration)
            || !call.TryReason(2, out var reason))
            return;

        if (target.Character is null)
        {
            call.Refuse(AdminText.NoCharacter(target.OocName));
            return;
        }

        conditions.Apply(target, _mute, duration, call.Sender.Ref, reason);
        audit.Log(MuteAction, call.Sender.Ref, target.Ref, new SanctionDetails((int?)duration?.TotalMinutes, reason));
        chat.SendTo(target, ChatLineKind.System, SanctionsText.MutedNotice(reason));
        call.Reply(duration is { } length
            ? SanctionsText.Muted(target.Name, (int)length.TotalMinutes)
            : SanctionsText.MutedUntilLifted(target.Name));
    }

    private void Unmute(CommandCall call)
    {
        if (!call.TryAccount(0, out var account))
            return;

        if (account.Online is not { } target)
        {
            fire.Run(UnmuteAction, () => lifts.LiftConditionAsync(call, account.ReadyMId, MuteKey, UnmuteAction,
                name => SanctionsText.NotMuted(name), name => SanctionsText.Unmuted(name)));
            return;
        }

        if (!call.MayPunish(target))
            return;

        if (!conditions.Remove(target, _mute))
        {
            call.Refuse(SanctionsText.NotMuted(target.Name));
            return;
        }

        audit.Log(UnmuteAction, call.Sender.Ref, target.Ref);
        chat.SendTo(target, ChatLineKind.System, SanctionsText.UnmutedNotice);
        call.Reply(SanctionsText.Unmuted(target.Name));
    }

    private static Phrase FormatRefusal(ActiveCondition mute) => mute.ExpiresAt is { } until
        ? SanctionsText.MuteDeny(Math.Max(1, (int)Math.Ceiling((until - DateTimeOffset.UtcNow).TotalMinutes)))
        : SanctionsText.MuteDenyUntilLifted;

    private sealed record SanctionDetails(int? Minutes, string Reason);
}
