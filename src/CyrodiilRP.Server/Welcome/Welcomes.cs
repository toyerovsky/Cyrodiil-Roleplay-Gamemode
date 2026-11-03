using System.Text.Json;
using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Welcome;

// The character stays where it stands until the player takes it over (Play) or has just made it (CharacterCreation).
[Service]
internal sealed partial class Welcomes(
    OnlinePlayers online,
    RpcHandlers rpc,
    RpChat chat,
    GoldDeliveries gold,
    Proximity proximity,
    IEntities entities,
    Timers timers)
{
    private const string ShowJob = "welcome.show";
    private const string HoldJob = "welcome.hold";

    // Long enough for the player's game to have its pages up when the welcome arrives.
    private static readonly TimeSpan JoinDelay = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan HoldEvery = TimeSpan.FromSeconds(1);

    private readonly PositionHold _hold = new(proximity, entities);

    private void Start()
    {
        online.Loaded += OnLoaded;
        online.Left += player => _hold.Release(player.Id);
        rpc.CharacterPlayed += OnPlayed;
        timers.RunEvery(HoldJob, HoldEvery, Hold);
    }

    private void OnLoaded(OnlinePlayer player) =>
        timers.RunAfter(ShowJob, JoinDelay, () =>
        {
            if (player.IsLive)
                rpc.SendWelcome(player.Id, JsonSerializer.Serialize(WelcomeCards.Build(player), WelcomeJson.Default.WelcomeView));
        });

    private void OnPlayed(PlayerId id)
    {
        if (!online.TryGet(id, out var player) || !CharacterSlots.MayPlay(player))
            return;

        player.IsPlaying = true;
        player.PlayCountedUntil = DateTimeOffset.UtcNow;
        _hold.Release(player.Id);
        rpc.SendWelcomeClosed(player.Id);
        chat.SendTo(player, ChatLineKind.System, CharactersText.WelcomeBack(player.Name));
        chat.Notify(player, CharactersText.WelcomeBack(player.Name));
        gold.Deliver(player);
    }

    private void Hold()
    {
        foreach (var player in online.All)
        {
            if (player.IsPlaying)
                _hold.Release(player.Id);
            else if (player.IsLoaded)
                _hold.Keep(player.Id);
        }
    }
}
