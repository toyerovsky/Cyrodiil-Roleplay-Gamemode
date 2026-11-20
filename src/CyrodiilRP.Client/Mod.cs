using CyrodiilRP.Client.Admin;
using CyrodiilRP.Client.Auction;
using CyrodiilRP.Client.Gambling;
using CyrodiilRP.Client.Asks;
using CyrodiilRP.Client.Away;
using CyrodiilRP.Client.Bank;
using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Creator;
using CyrodiilRP.Client.Dice;
using CyrodiilRP.Client.Exchange;
using CyrodiilRP.Client.Guide;
using CyrodiilRP.Client.Interactions;
using CyrodiilRP.Client.Jobs;
using CyrodiilRP.Client.Lore;
using CyrodiilRP.Client.Notices;
using CyrodiilRP.Client.Party;
using CyrodiilRP.Client.PlayerMenu;
using CyrodiilRP.Client.Specializations;
using CyrodiilRP.Client.Stable;
using CyrodiilRP.Client.Traders;
using CyrodiilRP.Client.Welcome;
using Microsoft.Extensions.Logging;
using ReadyM.Api.DI;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client;

[ModEntry]
internal sealed partial class Mod(IDependencyContainer services, ILogger logger)
{
    private void Init()
    {
        // The CEF registry finds page messages through ICefRpcHandler.
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<ChatOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<NameFormOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<RadialOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<ExchangeOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<PartyOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<AsksOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<TraderOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<ForemanOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<BankOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<AuctionOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<StableOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<MentorOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<BoardOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<GamblingOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<PlayerMenuOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<DiceOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<LoreOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<AdminOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<FrozenOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<WelcomeOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<GuideOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<AwayOverlay>());
        services.RegisterSingleton<ICefRpcHandler>(services.Resolve<PresenceReports>());

        logger.LogInformation("[CyrodiilRP] Client half up.");
    }
}
