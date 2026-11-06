using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.PlayerMenu;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Sanctuary;

[Service]
internal sealed partial class SanctuaryCommands(
    CommandRegistry commands,
    Guardian guardian,
    PlayerMenus menus,
    SelfMenu self,
    Audit audit)
{
    private const string StatusOption = "self.sanctuary";
    private const int StatusOrder = 65;

    private const string ClearWord = "clear";
    private const string OnWord = "on";
    private const string OffWord = "off";

    private const string ClearAction = "sanctuary.clear";
    private const string OnAction = "sanctuary.on";
    private const string OffAction = "sanctuary.off";

    private const int SecondsPerMinute = 60;

    private void Start()
    {
        commands.Add(new CommandSpec { Name = "sanctuary", Usage = SanctuaryText.CmdSanctuary, Category = CommandCategory.General }, ShowStatus);
        commands.AddStaff(StaffPermissions.Sanctuary, SanctuaryText.CmdAsanctuary, Manage);

        // Only where it matters: inside the Sanctuary, or while it holds a streak against the player.
        self.Add(new SelfOption
        {
            Id = StatusOption,
            Label = SanctuaryText.Option,
            Icon = WheelIcons.Shield,
            Order = StatusOrder,
            State = player => guardian.Describe(player) is { Enabled: true } status && (status.Inside || status.Aggressor)
                ? OptionState.Open
                : OptionState.Hidden,
            Run = player => menus.Show(player, MenuTabs.Sanctuary),
        });
    }

    public static Phrase FormatTime(int seconds) => SanctuaryText.Time(seconds / SecondsPerMinute, seconds % SecondsPerMinute);

    public static Phrase DescribeStatus(SanctuaryStatus status)
    {
        if (!status.Enabled)
            return SanctuaryText.StatusOff;

        if (status.Inside)
            return status.Aggressor
                ? SanctuaryText.StatusAggressor(status.Area, FormatTime(status.SecondsLeft))
                : SanctuaryText.StatusProtected(status.Area);

        return status.Aggressor ? SanctuaryText.StatusAggressorOutside(FormatTime(status.SecondsLeft)) : SanctuaryText.StatusOutside;
    }

    private void ShowStatus(CommandCall call) => call.Reply(DescribeStatus(guardian.Describe(call.Sender)));

    // /asanctuary alone lists the aggressors, /asanctuary 3 clear ends a streak, /asanctuary on|off turns it on or off.
    private void Manage(CommandCall call)
    {
        switch (call.Words.Count)
        {
            case 0:
                ListAggressors(call);
                return;

            case 1 when string.Equals(call.Words[0], OnWord, StringComparison.OrdinalIgnoreCase):
                Turn(call, true);
                return;

            case 1 when string.Equals(call.Words[0], OffWord, StringComparison.OrdinalIgnoreCase):
                Turn(call, false);
                return;

            case 2 when string.Equals(call.Words[1], ClearWord, StringComparison.OrdinalIgnoreCase):
                Clear(call);
                return;

            default:
                call.Refuse(SanctuaryText.Usage);
                return;
        }
    }

    private void ListAggressors(CommandCall call)
    {
        var state = guardian.IsOn ? SanctuaryText.StateOn : SanctuaryText.StateOff;
        var aggressors = guardian.ListAggressors();
        if (aggressors.Count == 0)
        {
            call.Reply(SanctuaryText.NoAggressors(state));
            return;
        }

        var entries = aggressors.Select(aggressor => SanctuaryText.AggressorEntry(aggressor.Player.Number, aggressor.Player.Name,
            FormatTime(aggressor.SecondsLeft)));
        call.Reply(SanctuaryText.Aggressors(state, entries.Aggregate((list, next) => SanctuaryText.ListMore(list, next))));
    }

    private void Turn(CommandCall call, bool enabled)
    {
        if (guardian.IsOn == enabled)
        {
            call.Refuse(SanctuaryText.Already(enabled ? SanctuaryText.StateOn : SanctuaryText.StateOff));
            return;
        }

        guardian.Turn(enabled);
        audit.Log(enabled ? OnAction : OffAction, call.Sender.Ref);
        call.Reply(enabled ? SanctuaryText.TurnedOn : SanctuaryText.TurnedOff);
    }

    private void Clear(CommandCall call)
    {
        if (!call.TryTarget(0, out var target))
            return;

        if (!guardian.TryClear(target))
        {
            call.Refuse(SanctuaryText.NotAggressor(target.Name));
            return;
        }

        audit.Log(ClearAction, call.Sender.Ref, target.Ref);
        call.Reply(SanctuaryText.Cleared(target.Name));
    }
}
