using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Staff;

// Only in memory: leaving ends it, so nobody stays on duty unseen after a reconnect. Nicknames marks the name.
[Service]
internal sealed partial class StaffDuty(CommandRegistry commands, OnlinePlayers online, StaffRoles roles, Audit audit, Callbacks callbacks, Timers timers)
{
    private const string StartAction = "staff.duty_start";
    private const string EndAction = "staff.duty_end";
    private const string ChangedCallback = "staff.duty";

    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    // A start or an end: what the staff member may do changed.
    public event Action<OnlinePlayer>? Changed;

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Duty, StaffText.CmdDuty, Toggle, offDuty: true);
        online.Left += player => End(player);
        timers.RunEvery("staff.duty_check", CheckEvery, EndWithoutPermission);
    }

    private void Toggle(CommandCall call)
    {
        if (End(call.Sender) is { } minutes)
        {
            call.Reply(StaffText.DutyOff(minutes));
            return;
        }

        call.Sender.DutySince = DateTimeOffset.UtcNow;
        audit.Log(StartAction, call.Sender.Ref);
        callbacks.Raise(ChangedCallback, Changed, call.Sender);
        call.Reply(StaffText.DutyOn);
    }

    // The minutes it lasted, null when the player was not on duty.
    private int? End(OnlinePlayer player)
    {
        if (player.DutySince is not { } since)
            return null;

        player.DutySince = null;
        var minutes = (int)Math.Round((DateTimeOffset.UtcNow - since).TotalMinutes);
        audit.Log(EndAction, player.Ref, details: new DutyDetails(minutes));
        if (player.IsLive)
            callbacks.Raise(ChangedCallback, Changed, player);

        return minutes;
    }

    // One whose role was taken (/aroles) could not end it with the command any more.
    private void EndWithoutPermission()
    {
        foreach (var player in online.All)
        {
            if (player.DutySince is not null && !roles.Allows(player, StaffPermissions.Duty))
                End(player);
        }
    }

    private sealed record DutyDetails(int Minutes);
}
