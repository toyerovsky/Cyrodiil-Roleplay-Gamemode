using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Traders;

// The client puts them over every trader, banker, stablemaster, mentor, registrar and auctioneer it sees.
[Service]
internal sealed partial class TraderLabels(RpcHandlers rpc, OnlinePlayers online, RpChat chat)
{
    private void Start()
    {
        online.Loaded += Send;
        online.LocaleChanged += Send;
    }

    private void Send(OnlinePlayer player)
    {
        rpc.SendTraderLabel(player.Id, chat.Translate(player, TraderText.Label));
        rpc.SendBankerLabel(player.Id, chat.Translate(player, BankText.Label));
        rpc.SendStablemasterLabel(player.Id, chat.Translate(player, StableText.Label));
        rpc.SendMentorLabels(player.Id,
            chat.Translate(player, SpecializationText.LabelWarriorMentor),
            chat.Translate(player, SpecializationText.LabelMageMentor),
            chat.Translate(player, SpecializationText.LabelSupportMentor),
            chat.Translate(player, SpecializationText.LabelRegistrar));
        rpc.SendAuctioneerLabel(player.Id, chat.Translate(player, AuctionText.Label));
    }
}
