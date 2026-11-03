using CyrodiilRP.Common.Chat;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Staff;

[Service]
internal sealed partial class StaffCommands(
    CommandRegistry commands,
    StaffRoles roles,
    OnlinePlayers online,
    Persistence persistence,
    RpChat chat,
    Audit audit,
    SelfMenu self,
    Callbacks callbacks,
    Fire fire)
{
    private const string StaffChatOption = "self.staff_chat";
    private const int StaffChatOrder = 100;

    private const string StaffChatAction = "staff.message";
    private const string MessageAction = "staff.message_to";
    private const string RolesAction = "staff.roles";
    private const string RolesChangedCallback = "staff.roles_changed";

    // What /aroles takes, and shows, for no roles at all.
    private const string NoRoles = "-";

    // The roles of a player online changed, and with them what they may do.
    public event Action<OnlinePlayer>? RolesChanged;

    private void Start()
    {
        commands.AddStaff(StaffPermissions.StaffChat, StaffText.CmdChat, SendStaffChat, offDuty: true);
        commands.AddStaff(StaffPermissions.Roles, StaffText.CmdRoles, ManageRoles);
        commands.AddStaff(StaffPermissions.Message, StaffText.CmdMessage, SendMessage);

        self.Add(new SelfOption
        {
            Id = StaffChatOption,
            Label = StaffText.OptionChat,
            Icon = WheelIcons.Shield,
            Order = StaffChatOrder,
            Permission = StaffPermissions.StaffChat,
            Run = player => chat.Prefill(player, $"/{StaffPermissions.StaffChat} "),
        });
    }

    private void SendStaffChat(CommandCall call)
    {
        if (call.Arguments.Length == 0)
        {
            call.Refuse(ChatText.Empty(StaffText.ExampleChat));
            return;
        }

        audit.Log(StaffChatAction, call.Sender.Ref, details: call.Arguments);
        foreach (var player in online.All.Where(p => roles.Allows(p, StaffPermissions.StaffChat)))
            chat.SendTo(player, ChatLineKind.Staff, StaffText.Chat(call.Sender.OocName, call.Arguments));
    }

    // To one player, in the chat and in the corner. The player answers with /pm: the staff chat is the staff's own.
    private void SendMessage(CommandCall call)
    {
        if (!call.TryTarget(0, out var target))
            return;

        var text = call.SkipWords(1).Trim();
        if (text.Length == 0)
        {
            call.Refuse(ChatText.Empty(StaffText.ExampleMessage));
            return;
        }

        audit.Log(MessageAction, call.Sender.Ref, target.Ref, text);
        chat.SendTo(target, ChatLineKind.Staff, StaffText.MessageReceived(call.Sender.OocName, text));
        chat.Notify(target, StaffText.MessageNotice(call.Sender.OocName));
        call.Reply(StaffText.MessageSent(target.Name, text));
    }

    private void ManageRoles(CommandCall call)
    {
        if (!call.TryAccount(0, out var target))
            return;

        var setting = call.Words.Count > 1;
        var wanted = call.Words.Skip(1).Where(word => word != NoRoles).Distinct(StringComparer.Ordinal).ToArray();
        fire.Run(RolesAction, () => ManageRolesAsync(call, target, setting, wanted));
    }

    private async Task ManageRolesAsync(CommandCall call, AccountTarget target, bool setting, string[] wanted)
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
        if (!setting)
        {
            call.Reply(StaffText.RolesOf(name, FormatRoles(account.Roles)));
            return;
        }

        if (wanted.FirstOrDefault(role => !roles.IsKnown(role)) is { } unknown)
        {
            call.Refuse(StaffText.UnknownRole(unknown, string.Join(", ", roles.RoleNames)));
            return;
        }

        var changed = account.Roles.Except(wanted).Concat(wanted.Except(account.Roles));
        if (changed.FirstOrDefault(role => !roles.MayChange(call.Sender, role)) is { } refused)
        {
            call.Refuse(StaffText.CannotGrant(refused));
            return;
        }

        await persistence.Writer.EnqueueAsync(RolesAction, (db, ct) => StaffBook.SetRolesAsync(db, target.ReadyMId, wanted, ct));
        if (online.TryFind(target.ReadyMId, out var player))
        {
            player.Roles = roles.ResolveRoles(target.ReadyMId, wanted);
            callbacks.Raise(RolesChangedCallback, RolesChanged, player);
        }

        audit.Log(RolesAction, call.Sender.Ref, OnlinePlayer.FormatRef(target.ReadyMId), new RolesDetails(FormatRoles(account.Roles), FormatRoles(wanted)));
        if (call.Sender.IsLive)
            call.Reply(StaffText.RolesSet(name, FormatRoles(wanted)));
    }

    private static string FormatRoles(string[] names) => names.Length == 0 ? NoRoles : string.Join(", ", names);

    private sealed record RolesDetails(string From, string To);
}
