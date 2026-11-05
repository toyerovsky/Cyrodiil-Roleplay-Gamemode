using System.Text.Json;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Conditions;
using CyrodiilRP.Server.Dice;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.PlayerMenu;

// Built from what the server already holds, so opening it reads nothing from the database.
[Service]
internal sealed partial class PlayerMenus(
    RpcHandlers rpc,
    OnlinePlayers online,
    CommandRegistry commands,
    CharacterConditions conditions,
    Wallet wallet,
    Rolls dice,
    LoreCards cards,
    LoreCardViews lore,
    Introductions introductions,
    ShownNames names,
    Localizer translations)
{
    private void Start()
    {
        online.Loaded += player => rpc.SendLocale(player.Id, player.Locale);
        rpc.MenuAsked += OnAsked;
        rpc.LanguageChosen += OnLanguageChosen;
        commands.Add(new CommandSpec { Name = "help", Usage = ChatText.CmdHelp, Category = CommandCategory.General, NeedsCharacter = true },
            call => Show(call.Sender, MenuTabs.Help));
    }

    private void OnAsked(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            Show(player, MenuTabs.Character);
    }

    private void OnLanguageChosen(PlayerId id, string locale)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded || !translations.HasLocale(locale))
            return;

        if (locale != player.Locale)
        {
            online.ChangeLocale(player, locale);
            rpc.SendLocale(player.Id, locale);
        }

        Show(player, MenuTabs.Account);
    }

    // Only with a character: before it, the name form holds the screen.
    public void Show(OnlinePlayer player, string tab)
    {
        if (!player.IsLoaded || player.Character is not { } character)
            return;

        string Translate(Phrase phrase) => translations.Get(player.Locale, phrase);
        int? gold = wallet.TryCount(player.Id, out var carried) ? carried : null;
        var view = new MenuView(
            tab,
            MenuCards.BuildCharacter(player, character, gold, DateTimeOffset.UtcNow),
            MenuCards.BuildAccount(player, conditions.ListTitled(player), Translate),
            HelpListing.Build(commands.ListAvailable(player), Translate),
            dice.BuildView(player),
            BuildLore(player),
            translations.Locales,
            player.Locale);
        rpc.SendMenuState(player.Id, JsonSerializer.Serialize(view, MenuJson.Default.MenuView));
    }

    private MenuLoreView BuildLore(OnlinePlayer player)
    {
        cards.Find(player, out var card);
        var known = introductions.ListKnowers(player)
            .OrderBy(other => other.Number)
            .Select(other => new MenuKnownView(other.Number, names.Shown(player, other)))
            .ToList();
        return new MenuLoreView(card.NamePublic, lore.BuildStrangerHeader(player), lore.BuildPreview(player, LoreViewer.Stranger), known);
    }
}
