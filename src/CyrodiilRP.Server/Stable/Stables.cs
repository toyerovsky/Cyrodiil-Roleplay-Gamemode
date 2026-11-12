using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Stable;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Traders;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Stable;

// The stable's window at a stablemaster, who is stood and kept as a trader is (Traders/). A character owns one horse, and
// buying another replaces it with nothing paid back. The gold leaves the purse before the purchase is written, and goes
// back if the write fails.
[Service]
internal sealed partial class Stables(
    RpcHandlers rpc,
    OnlinePlayers online,
    InteractionMenu interactions,
    Proximity proximity,
    TraderNpcs npcs,
    Horses horses,
    Wallet wallet,
    Persistence persistence,
    RpConfig config,
    RpChat chat,
    Audit audit,
    Timers timers,
    Fire fire,
    SystemClock clock,
    ILogger logger) : IFrontTarget
{
    private const string OpenJob = "stable.open";
    private const string BuyAction = "economy.horse_buy";
    private const string WatchJob = "stable.watch";

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    private readonly Dictionary<OnlinePlayer, OpenStable> _open = [];

    // The players whose purchase is still being written: a second one would take the gold again.
    private readonly HashSet<OnlinePlayer> _buying = [];

    public float? MeasureInFront(OnlinePlayer actor, float reach, float coneDegrees) =>
        FindInFront(actor, reach, coneDegrees, out var distance) is null ? null : distance;

    public void Use(OnlinePlayer actor, float reach, float coneDegrees)
    {
        if (FindInFront(actor, reach, coneDegrees, out _) is { } spot && actor.Character is { } character)
            Open(actor, character.Id, spot);
    }

    private void Start()
    {
        interactions.AddTarget(this);
        rpc.StableBought += OnBought;
        rpc.StableLeft += OnLeftWindow;
        online.Left += player =>
        {
            _open.Remove(player);
            _buying.Remove(player);
        };
        timers.RunEvery(WatchJob, WatchEvery, Watch);
    }

    // The key is the stable's only when a stablemaster is the nearest of the mode's people in front.
    private TraderSpotRecord? FindInFront(OnlinePlayer actor, float reach, float coneDegrees, out float distance) =>
        npcs.FindNearestInFront(actor, reach, coneDegrees, out distance) is { Kind: TraderSpotKind.Stablemaster } spot ? spot : null;

    // The horse owned is read again, so the window never sells on a guess.
    private void Open(OnlinePlayer player, long characterId, TraderSpotRecord spot) => fire.Run(OpenJob, async () =>
    {
        try
        {
            await horses.RefreshAsync(player, characterId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] The horse of {Character} could not be read.", CharacterRecord.FormatRef(characterId));
            if (player.IsLive)
                chat.Notify(player, StableText.Unreadable);

            return;
        }

        if (!player.IsLive)
            return;

        if (CheckWindow(player, spot) is { } broken)
        {
            chat.Notify(player, broken);
            return;
        }

        _open[player] = new OpenStable(spot, characterId, SeenGold: 0);
        SendState(player);
    });

    private void OnBought(PlayerId id, string name)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        if (!StableRules.TryRead(name, out var kind))
        {
            logger.LogWarning("[CyrodiilRP] {Player} asked the stable for a horse {Horse}, which it does not sell.", player.Ref, name);
            Answer(player, StableText.UnknownHorse, false);
            return;
        }

        if (_buying.Contains(player))
        {
            Answer(player, StableText.Busy, false);
            return;
        }

        var horse = StableRules.FindName(kind);
        var owned = horses.FindOwned(player);
        if (owned?.Kind == kind)
        {
            Answer(player, StableText.AlreadyYours(horse), false);
            return;
        }

        var price = config.Stable.FindPrice(kind);
        if (!wallet.TryCount(player.Id, out var gold) || gold < price || (price > 0 && !wallet.TryTake(player.Id, price)))
        {
            Answer(player, StableText.TooPoor(horse, price, gold), false);
            return;
        }

        var characterId = open.CharacterId;
        var at = clock.UtcNow;
        _buying.Add(player);
        fire.Run(BuyAction, async () =>
        {
            try
            {
                await persistence.Writer.EnqueueAsync(BuyAction, (db, ct) => StableBook.BuyAsync(db, characterId, kind, price, at, ct));
            }
            catch (Exception exception)
            {
                _buying.Remove(player);
                logger.LogError(exception, "[CyrodiilRP] {Player} could not buy a {Horse} horse.", player.Ref, kind);
                var refunded = price == 0 || (player.IsLive && wallet.TryGive(player.Id, price));
                if (!refunded)
                    logger.LogError("[CyrodiilRP] {Player} paid {Price} gold for a horse that was not written, and could not be given it back.", player.Ref, price);

                Finish(player, refunded ? StableText.Failed : StableText.Lost, bought: false);
                return;
            }

            _buying.Remove(player);
            if (player.IsLive)
                horses.Own(player, characterId, kind);

            audit.Log(BuyAction, player.Ref, CharacterRecord.FormatRef(characterId), new PurchaseDetails(kind, price, owned?.Kind));
            Finish(player, owned is { } before ? StableText.BoughtInstead(horse, price, StableRules.FindName(before.Kind)) : StableText.Bought(horse, price),
                bought: true);
        });
    }

    // False once the window closed or was never opened. One that no longer holds is closed here.
    private bool TryFindOpen(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out OpenStable? open)
    {
        open = null;
        if (!online.TryGet(id, out player) || !_open.TryGetValue(player, out open))
            return false;

        if (CheckWindow(player, open.Spot) is not { } broken)
            return true;

        Close(player, broken);
        return false;
    }

    // Onto the window, or into the corner when it closed while the purchase was being written.
    private void Finish(OnlinePlayer player, Phrase text, bool bought)
    {
        if (!player.IsLive)
            return;

        if (!_open.ContainsKey(player))
        {
            chat.Notify(player, text);
            return;
        }

        Answer(player, text, bought);
        SendState(player);
    }

    private void OnLeftWindow(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    // A stablemaster the staff took away takes its windows with it here, within a second.
    private void Watch()
    {
        if (_open.Count == 0)
            return;

        foreach (var (player, open) in _open.ToList())
        {
            if (CheckWindow(player, open.Spot) is { } broken)
                Close(player, broken);
            else if (wallet.TryCount(player.Id, out var gold) && gold != open.SeenGold)
                SendState(player);
        }
    }

    private Phrase? CheckWindow(OnlinePlayer player, TraderSpotRecord spot)
    {
        if (!wallet.IsInWorld(player.Id))
            return StableText.ClosedNotInWorld;

        if (!npcs.TryFind(spot, out var npc))
            return StableText.ClosedGone;

        return proximity.MeasureDistance(player.Id, new AreaId(spot.Area), npc.Position) is { } away && away <= TraderRules.WalkAway
            ? null
            : StableText.ClosedFar(TraderRules.WalkAwayMetres);
    }

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;

        rpc.SendStableClosed(player.Id);
        chat.Notify(player, why);
    }

    private void Answer(OnlinePlayer player, Phrase text, bool bought) => rpc.SendStableAnswer(player.Id, chat.Translate(player, text), bought);

    private void SendState(OnlinePlayer player)
    {
        wallet.TryCount(player.Id, out var gold);
        _open[player] = _open[player] with { SeenGold = gold };
        var owned = horses.FindOwned(player);
        var offered = Enum.GetValues<HorseKind>()
            .Select(kind => new StableHorseView(kind, config.Stable.FindPrice(kind), owned?.Kind == kind))
            .ToList();
        rpc.SendStableState(player.Id, JsonSerializer.Serialize(new StableView(offered, gold), StableJson.Default.StableView));
    }

    private sealed record PurchaseDetails(HorseKind Horse, int Price, HorseKind? Replaced);
}
