using System.Text.Json;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Sanctuary;

// What the Sanctuary tells: the players in the corner and on their badge, the staff on duty in the corner. None of it
// names another player to a player, so nobody's shown name is needed. The staff read real names.
[Service]
internal sealed partial class SanctuaryNotices(
    RpChat chat,
    RpcHandlers rpc,
    OnlinePlayers online,
    StaffRoles roles,
    Localizer texts,
    ILogger logger)
{
    public void Zone(OnlinePlayer player, bool inside) => chat.Notify(player, inside ? SanctuaryText.Enter : SanctuaryText.Leave);

    public void Shielded(OnlinePlayer player) => chat.Notify(player, SanctuaryText.Shielded);

    public void Aggressor(OnlinePlayer player) => chat.Notify(player, SanctuaryText.Aggressor);

    public void ProtectedAgain(OnlinePlayer player) => chat.Notify(player, SanctuaryText.ProtectedAgain);

    public void Badge(OnlinePlayer player, SanctuaryHudView view) =>
        rpc.SendSanctuaryState(player.Id, JsonSerializer.Serialize(view, SanctuaryJson.Default.SanctuaryHudView));

    public void TellStaff(Phrase text)
    {
        logger.LogInformation("[CyrodiilRP] {Text}", texts.Get(Localizer.BaseLocale, text));
        foreach (var player in online.All)
        {
            if (player.IsLoaded && player.DutySince is not null && roles.Allows(player, StaffPermissions.Sanctuary))
                chat.Notify(player, text);
        }
    }
}
