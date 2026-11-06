using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Dice;
using CyrodiilRP.Server.Faction;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Admin;

// A button runs its command through the registry, so its permission, duty and audit are the command's own.
[Service]
internal sealed partial class AdminPanel(
    CommandRegistry commands,
    StaffRoles roles,
    StaffDuty duty,
    StaffCommands staff,
    OnlinePlayers online,
    AdminViews views,
    RpcHandlers rpc,
    RpChat chat,
    SelfMenu self,
    InteractionMenu interactions,
    Fire fire,
    DiceLog rolls,
    LoreCardViews lore,
    LoreCards cards,
    Factions factions,
    ILogger logger) : ICommandOutput
{
    private const string PanelOption = "self.admin";
    private const string CardOption = "player.admin";
    private const int PanelOrder = 105;
    private const int CardOrder = 105;

    private const string CardJob = "admin.card";
    private const string SanctionsJob = "admin.sanctions";
    private const string AuditJob = "admin.audit";
    private const string RollsJob = "admin.rolls";

    // Whose panel is open: their answers go to it, and it follows the players, the duty and the roles.
    private readonly HashSet<PlayerId> _open = [];

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Panel, AdminText.CmdPanel, Open, offDuty: true);

        rpc.AdminRan += OnRan;
        rpc.AdminAsked += OnAsked;
        rpc.AdminClosed += id => _open.Remove(id);
        online.Loaded += _ => RefreshAll();
        online.Left += player =>
        {
            _open.Remove(player.Id);
            RefreshAll();
        };
        duty.Changed += _ => RefreshAll();
        staff.RolesChanged += _ => RefreshAll();

        self.Add(new SelfOption
        {
            Id = PanelOption,
            Label = AdminText.OptionPanel,
            Icon = WheelIcons.Card,
            Order = PanelOrder,
            Permission = StaffPermissions.Panel,
            Run = player => commands.Run(player, $"/{StaffPermissions.Panel}", chat),
        });
        interactions.Add(new InteractionOption
        {
            Id = CardOption,
            Label = AdminText.OptionCard,
            Icon = WheelIcons.Card,
            Order = CardOrder,
            Permission = StaffPermissions.Panel,
            Run = (actor, target) => commands.Run(actor, $"/{StaffPermissions.Panel} {target.Number}", chat),
        });
    }

    // Into the panel while it is open, into the chat when it is not: an answer that comes after it closed is still read.
    public void Reply(OnlinePlayer to, Phrase text) => Answer(to, text, done: true);

    public void Refuse(OnlinePlayer to, Phrase text) => Answer(to, text, done: false);

    private void Answer(OnlinePlayer to, Phrase text, bool done)
    {
        if (!_open.Contains(to.Id) || !roles.Allows(to, StaffPermissions.Panel))
        {
            Close(to);
            if (done)
                chat.Reply(to, text);
            else
                chat.Refuse(to, text);
            return;
        }

        rpc.SendAdminAnswer(to.Id, chat.Translate(to, text), done);
        SendState(to, focus: null);
    }

    private void Open(CommandCall call)
    {
        int? focus = null;
        if (call.Words.Count > 0)
        {
            if (!call.TryTarget(0, out var target))
                return;

            focus = target.Number;
        }

        _open.Add(call.Sender.Id);
        SendState(call.Sender, focus);
    }

    private bool IsOpenFor(PlayerId id, out OnlinePlayer player) =>
        online.TryGet(id, out player!) && player.IsLoaded && _open.Contains(id) && roles.Allows(player, StaffPermissions.Panel);

    // Only the commands of the buttons: the panel is not a second chat.
    private void OnRan(PlayerId id, string line)
    {
        if (!IsOpenFor(id, out var player))
            return;

        if (!CommandLine.TryParse(line, out var name, out _) || !AdminActions.Commands.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning("[CyrodiilRP] {Player}'s panel asked to run {Line}, which no button runs.", id, line);
            return;
        }

        commands.Run(player, line, this);
    }

    private void OnAsked(PlayerId id, string kind, string argument)
    {
        if (!IsOpenFor(id, out var player))
            return;

        switch (kind)
        {
            case AdminParts.Card:
                if (int.TryParse(argument, out var number) && number is >= 0 and <= ushort.MaxValue && online.TryFind((ushort)number, out var target))
                    fire.Run(CardJob, () => SendCardAsync(player, target));
                break;

            case AdminParts.Sanctions:
                fire.Run(SanctionsJob, () => SendSanctionsAsync(player));
                break;

            case AdminParts.Audit:
                if (Parse(argument, AdminJson.Default.AdminAuditFilter) is { } filter)
                    fire.Run(AuditJob, () => SendAuditAsync(player, filter));
                break;

            case AdminParts.Rolls:
                if (Parse(argument, AdminJson.Default.AdminRollFilter) is { } rollFilter)
                    fire.Run(RollsJob, () => SendRollsAsync(player, rollFilter));
                break;

            case AdminParts.Lore:
                if (int.TryParse(argument, out var owner) && owner is >= 0 and <= ushort.MaxValue && online.TryFind((ushort)owner, out var subject)
                    && subject.Character is not null)
                    SendLore(player, subject);
                break;

            case AdminParts.Factions:
                SendFactions(player);
                break;

            case AdminParts.Faction:
                if (long.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var factionId))
                    SendFaction(player, factionId);
                break;

            case AdminParts.Items:
                if (Parse(argument, AdminJson.Default.AdminItemFilter) is { } items)
                    rpc.SendAdminPart(player.Id, AdminParts.Items, JsonSerializer.Serialize(AdminViews.SearchItems(items), AdminJson.Default.ListAdminItemView));
                break;
        }
    }

    private void RefreshAll()
    {
        foreach (var id in _open.ToList())
        {
            if (online.TryGet(id, out var player))
                SendState(player, focus: null);
        }
    }

    // One whose role /aroles took gets the panel closed instead.
    private void SendState(OnlinePlayer player, int? focus)
    {
        if (!player.IsLive)
            return;

        if (!roles.Allows(player, StaffPermissions.Panel))
        {
            Close(player);
            return;
        }

        var view = views.BuildState(player, focus);
        rpc.SendAdminState(player.Id, JsonSerializer.Serialize(view, AdminJson.Default.AdminView));
    }

    private void Close(OnlinePlayer player)
    {
        if (_open.Remove(player.Id))
            rpc.SendAdminClosed(player.Id);
    }

    private async Task SendCardAsync(OnlinePlayer staff, OnlinePlayer target)
    {
        var card = await views.ReadCardAsync(target);
        if (card is null || !staff.IsLive)
            return;

        rpc.SendAdminPart(staff.Id, AdminParts.Card, JsonSerializer.Serialize(card, AdminJson.Default.AdminCardView));
    }

    private async Task SendSanctionsAsync(OnlinePlayer staff)
    {
        var sanctions = await views.ReadSanctionsAsync();
        if (!staff.IsLive)
            return;

        rpc.SendAdminPart(staff.Id, AdminParts.Sanctions, JsonSerializer.Serialize(sanctions, AdminJson.Default.ListAdminSanctionView));
    }

    private async Task SendAuditAsync(OnlinePlayer staff, AdminAuditFilter filter)
    {
        var page = await views.ReadAuditAsync(filter);
        if (!staff.IsLive)
            return;

        rpc.SendAdminPart(staff.Id, AdminParts.Audit, JsonSerializer.Serialize(page, AdminJson.Default.AdminAuditView));
    }

    private async Task SendRollsAsync(OnlinePlayer staff, AdminRollFilter filter)
    {
        var page = await rolls.ReadPageAsync(filter);
        if (!staff.IsLive)
            return;

        rpc.SendAdminPart(staff.Id, AdminParts.Rolls, JsonSerializer.Serialize(page, AdminJson.Default.AdminRollsView));
    }

    private void SendLore(OnlinePlayer staff, OnlinePlayer subject)
    {
        cards.Find(subject, out var card);
        var view = new AdminLoreView(subject.Number, subject.Name, card.NamePublic, lore.BuildStaffView(staff, subject));
        rpc.SendAdminPart(staff.Id, AdminParts.Lore, JsonSerializer.Serialize(view, AdminJson.Letters.AdminLoreView));
    }

    private void SendFactions(OnlinePlayer staff)
    {
        var list = FactionViews.BuildStaffList(factions.All, factions.MapOnline());
        rpc.SendAdminPart(staff.Id, AdminParts.Factions, JsonSerializer.Serialize(list, AdminJson.Letters.ListStaffFactionView));
    }

    // One dissolved since the list was read comes back as its id alone, so the page drops it.
    private void SendFaction(OnlinePlayer staff, long id)
    {
        var json = factions.Find(id) is { } faction
            ? JsonSerializer.Serialize(FactionViews.BuildStaffCard(faction, factions.MapOnline()), AdminJson.Letters.StaffFactionCardView)
            : JsonSerializer.Serialize(id, AdminJson.Letters.Int64);
        rpc.SendAdminPart(staff.Id, AdminParts.Faction, json);
    }

    private T? Parse<T>(string json, JsonTypeInfo<T> type) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize(json, type);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "[CyrodiilRP] The panel sent a {Type} that is no JSON.", typeof(T).Name);
            return null;
        }
    }
}
