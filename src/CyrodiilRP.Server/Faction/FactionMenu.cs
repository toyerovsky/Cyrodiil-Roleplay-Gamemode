using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.PlayerMenu;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Faction;

// The buttons of the menu's faction tab run /faction as if typed, so their checks are the command's own. The answer
// goes back with the menu as it is after it. The player's game puts both on the page, or the answer in the corner when
// the menu has closed since.
[Service]
internal sealed partial class FactionMenu(
    RpcHandlers rpc,
    OnlinePlayers online,
    CommandRegistry commands,
    RpChat chat,
    PlayerMenus menus,
    ILogger logger) : ICommandOutput
{
    public void Reply(OnlinePlayer to, Phrase text) => Answer(to, text, done: true);

    public void Refuse(OnlinePlayer to, Phrase text) => Answer(to, text, done: false);

    private void Start() => rpc.FactionRan += OnRan;

    // Only /faction: the tab is not a second chat.
    private void OnRan(PlayerId id, string line)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded)
            return;

        if (line.Length > ChatLimits.MaxMessageLength || !CommandLine.TryParse(line, out var name, out _)
                                                      || !string.Equals(name, FactionCommands.Command, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[CyrodiilRP] {Player}'s menu asked to run a line of {Length} characters that is no /faction.", id, line.Length);
            return;
        }

        if (chat.CountLine(player) is { } tooFast)
        {
            Refuse(player, tooFast);
            return;
        }

        commands.Run(player, line, this);
    }

    private void Answer(OnlinePlayer to, Phrase text, bool done)
    {
        if (to.IsLive && menus.BuildJson(to, MenuTabs.Faction) is { } menu)
            rpc.SendFactionAnswer(to.Id, chat.Translate(to, text), done, menu);
    }
}
