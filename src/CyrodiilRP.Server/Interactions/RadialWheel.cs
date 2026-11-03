using System.Text.Json;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Interactions;

// One wheel a player: a new one replaces the one before.
[Service]
internal sealed partial class RadialWheel(RpcHandlers rpc, OnlinePlayers online)
{
    private readonly Dictionary<OnlinePlayer, WheelRequest> _open = [];

    public void Open(OnlinePlayer player, WheelRequest request)
    {
        if (!player.IsLive)
            return;

        _open[player] = request;
        rpc.SendShowWheel(player.Id, JsonSerializer.Serialize(new WheelView(request.Title, request.Items), WheelJson.Default.WheelView));
    }

    private void Start()
    {
        rpc.WheelPicked += OnPicked;
        rpc.WheelClosed += OnClosed;
        online.Left += player => _open.Remove(player);
    }

    private void OnPicked(PlayerId id, string itemId)
    {
        if (!online.TryGet(id, out var player) || !_open.Remove(player, out var request))
            return;

        if (request.Items.FirstOrDefault(item => item.Id == itemId) is { Disabled: false } item)
            request.OnPick(player, item.Id);
    }

    private void OnClosed(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }
}
