using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.PlayerMenu;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Lore;

[Service]
internal sealed partial class LoreCommands(
    CommandRegistry commands,
    LoreWindows windows,
    LoreCards cards,
    LoreDesk desk,
    SelfMenu self,
    InteractionMenu interactions,
    PlayerMenus menus,
    RpcHandlers rpc,
    OnlinePlayers online,
    RpChat chat,
    Audit audit)
{
    private const string CardOption = "self.lore";
    private const string ReadOption = "player.lore";
    private const string ClearAction = "admin.lore_clear";
    private const string ClearWord = "clear";

    private const int CardOrder = 25;
    private const int ReadOrder = 11;

    private void Start()
    {
        commands.Add(new CommandSpec { Name = "mylore", Usage = LoreText.CmdMylore, Category = CommandCategory.Roleplay, NeedsCharacter = true },
            call => windows.ToggleEditor(call.Sender));
        commands.Add(new CommandSpec { Name = "lore", Usage = LoreText.CmdLore, Category = CommandCategory.Roleplay, NeedsCharacter = true }, Read);
        commands.Add(new CommandSpec { Name = "name", Usage = LoreText.CmdName, Category = CommandCategory.Roleplay, NeedsCharacter = true }, ChooseName);
        commands.AddStaff(StaffPermissions.Lore, LoreText.CmdAlore, ClearForStaff);

        self.Add(new SelfOption { Id = CardOption, Label = LoreText.OptionCard, Icon = WheelIcons.Book, Order = CardOrder, Run = windows.OpenEditor });
        interactions.Add(new InteractionOption
        {
            Id = ReadOption,
            Label = LoreText.OptionRead,
            Icon = WheelIcons.Book,
            Order = ReadOrder,
            Run = windows.OpenCard,
        });
        rpc.NamePublicChosen += OnNamePublicChosen;
    }

    private void Read(CommandCall call)
    {
        if (call.Words.Count == 0)
        {
            windows.OpenInFront(call.Sender);
            return;
        }

        if (call.TryTarget(0, out var target))
            windows.OpenCard(call.Sender, target);
    }

    private void ChooseName(CommandCall call)
    {
        bool? namePublic = call.Words.Count == 1 ? call.Words[0].ToLowerInvariant() switch
        {
            "public" => true,
            "private" => false,
            _ => null,
        } : null;
        if (namePublic is not { } chosen)
        {
            call.Refuse(LoreText.NameUsage);
            return;
        }

        if (SetNamePublic(call.Sender, chosen) is { } refused)
        {
            call.Refuse(refused);
            return;
        }

        call.Reply(chosen ? LoreText.NamePublic : LoreText.NamePrivate);
    }

    // The menu's lore tab comes back with the choice made, or the chat says why not.
    private void OnNamePublicChosen(PlayerId id, bool namePublic)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded)
            return;

        if (SetNamePublic(player, namePublic) is { } refused)
            chat.Refuse(player, refused);

        menus.Show(player, MenuTabs.Lore);
    }

    // Null when done.
    private Phrase? SetNamePublic(OnlinePlayer player, bool namePublic)
    {
        if (LoreCards.FindRefusal(cards.Find(player, out _)) is { } refused)
            return refused;

        cards.SetNamePublic(player, namePublic);
        return null;
    }

    // /alore 3 clear history: the staff take out what should not be on a card.
    private void ClearForStaff(CommandCall call)
    {
        if (!call.TryTarget(0, out var target))
            return;

        if (call.Words.Count != 3 || !string.Equals(call.Words[1], ClearWord, StringComparison.OrdinalIgnoreCase)
            || !LoreFields.TryGet(call.Words[2], out var field))
        {
            call.Refuse(LoreText.AloreUsage);
            return;
        }

        if (!call.MayPunish(target, self: true))
            return;

        if (LoreCards.FindRefusal(cards.Find(target, out var card)) is { } refused)
        {
            call.Refuse(refused);
            return;
        }

        var edit = desk.Editor.ClearForStaff(card, field);
        if (!edit.Ok)
        {
            call.Refuse(edit.Message);
            return;
        }

        cards.Save(target, edit);
        audit.Log(ClearAction, call.Sender.Ref, target.Ref, new ClearDetails(field.Id));
        if (target != call.Sender)
            chat.Notify(target, LoreText.ClearedByStaff(field.Label));

        call.Reply(LoreText.StaffCleared(field.Label, target.Name));
    }

    private sealed record ClearDetails(string Field);
}
