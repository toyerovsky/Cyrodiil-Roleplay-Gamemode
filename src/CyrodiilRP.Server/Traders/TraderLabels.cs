using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Traders;

// The client puts it over every trader it sees.
[Service]
internal sealed partial class TraderLabels(RpcHandlers rpc, OnlinePlayers online, RpChat chat)
{
    private void Start()
    {
        online.Loaded += Send;
        online.LocaleChanged += Send;
    }

    private void Send(OnlinePlayer player) => rpc.SendTraderLabel(player.Id, chat.Translate(player, TraderText.Label));
}
