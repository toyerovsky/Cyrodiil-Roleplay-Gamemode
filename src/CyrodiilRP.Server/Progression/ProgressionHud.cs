using System.Text.Json;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Progression;

[Service]
internal sealed partial class ProgressionHud(OnlinePlayers online, RpcHandlers rpc)
{
    private readonly Dictionary<OnlinePlayer, ProgressionView> _shown = [];

    private void Start() => online.Left += player => _shown.Remove(player);

    public void Show(OnlinePlayer player, ProgressionView view)
    {
        if (_shown.TryGetValue(player, out var shown) && shown == view)
            return;
        _shown[player] = view;
        rpc.SendProgressionState(player.Id, JsonSerializer.Serialize(view, ProgressionJson.Default.ProgressionView));
    }
}
