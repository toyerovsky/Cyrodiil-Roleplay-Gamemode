using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CyrodiilRP.Data.Bank;
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

namespace CyrodiilRP.Server.Bank;

// The bank's window at a banker, who is stood and kept as a trader is (Traders/). Gold moves between what the character
// carries and their account, and never exists in both: a deposit leaves the purse before it is written, a withdrawal is
// written before it reaches the purse, and whichever half fails is undone.
[Service]
internal sealed partial class Bankers(
    RpcHandlers rpc,
    OnlinePlayers online,
    InteractionMenu interactions,
    Proximity proximity,
    TraderNpcs npcs,
    BankAccounts accounts,
    Wallet wallet,
    RpChat chat,
    Audit audit,
    Timers timers,
    Fire fire,
    ILogger logger) : IFrontTarget
{
    private const string OpenJob = "bank.open";
    private const string DepositAction = "economy.bank_deposit";
    private const string WithdrawAction = "economy.bank_withdraw";
    private const string WatchJob = "bank.watch";

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    private readonly Dictionary<OnlinePlayer, OpenBank> _open = [];

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
        rpc.BankDeposited += OnDeposited;
        rpc.BankWithdrawn += OnWithdrawn;
        rpc.BankLeft += OnLeftWindow;
        online.Left += player => _open.Remove(player);
        timers.RunEvery(WatchJob, WatchEvery, Watch);
    }

    // The key is the bank's only when a banker is the nearest of the mode's people in front, not a trader.
    private TraderSpotRecord? FindInFront(OnlinePlayer actor, float reach, float coneDegrees, out float distance) =>
        npcs.FindNearestInFront(actor, reach, coneDegrees, out distance) is { Kind: TraderSpotKind.Banker } spot ? spot : null;

    private void Open(OnlinePlayer player, long characterId, TraderSpotRecord spot) => fire.Run(OpenJob, async () =>
    {
        long balance;
        try
        {
            balance = await accounts.ReadBalanceAsync(characterId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] The bank account of {Character} could not be read.", CharacterRecord.FormatRef(characterId));
            if (player.IsLive)
                chat.Notify(player, BankText.Unreadable);

            return;
        }

        if (!player.IsLive)
            return;

        if (CheckWindow(player, spot) is { } broken)
        {
            chat.Notify(player, broken);
            return;
        }

        _open[player] = new OpenBank(spot, characterId, balance, SeenGold: 0);
        SendState(player);
    });

    private void OnDeposited(PlayerId id, int amount)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        if (!BankRules.Fits(amount))
        {
            Answer(player, BankText.Amount(BankRules.MostAtOnce), false);
            return;
        }

        if (!wallet.TryCount(player.Id, out var gold) || gold < amount || !wallet.TryTake(player.Id, amount))
        {
            Answer(player, BankText.TooPoor(amount, gold), false);
            return;
        }

        fire.Run(DepositAction, async () =>
        {
            long balance;
            try
            {
                balance = await accounts.DepositAsync(open.CharacterId, amount);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not deposit {Amount} gold.", player.Ref, amount);
                var refunded = player.IsLive && wallet.TryGive(player.Id, amount);
                if (!refunded)
                    logger.LogError("[CyrodiilRP] {Player} gave {Amount} gold to a deposit that was not written, and could not be given it back.", player.Ref, amount);

                Finish(player, null, refunded ? BankText.Failed : BankText.Unreadable, done: false);
                return;
            }

            audit.Log(DepositAction, player.Ref, CharacterRecord.FormatRef(open.CharacterId), new MovementDetails(amount, balance));
            Finish(player, balance, BankText.Deposited(amount), done: true);
        });
    }

    private void OnWithdrawn(PlayerId id, int amount)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        if (!BankRules.Fits(amount))
        {
            Answer(player, BankText.Amount(BankRules.MostAtOnce), false);
            return;
        }

        fire.Run(WithdrawAction, async () =>
        {
            BankWithdrawal withdrawal;
            try
            {
                withdrawal = await accounts.WithdrawAsync(open.CharacterId, amount);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not withdraw {Amount} gold.", player.Ref, amount);
                Finish(player, null, BankText.Failed, done: false);
                return;
            }

            if (!withdrawal.Done)
            {
                Finish(player, withdrawal.Balance, BankText.TooLittle(amount, withdrawal.Balance), done: false);
                return;
            }

            if (!player.IsLive || !wallet.TryGive(player.Id, amount))
            {
                await PutBackAsync(player, open.CharacterId, amount);
                return;
            }

            audit.Log(WithdrawAction, player.Ref, CharacterRecord.FormatRef(open.CharacterId), new MovementDetails(amount, withdrawal.Balance));
            Finish(player, withdrawal.Balance, BankText.Withdrawn(amount), done: true);
        });
    }

    // A withdrawal that could not reach the purse (the player left, or carries all they can) goes back into the account.
    private async Task PutBackAsync(OnlinePlayer player, long characterId, int amount)
    {
        long restored;
        try
        {
            restored = await accounts.DepositAsync(characterId, amount);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] {Amount} gold left the account of {Character} and could neither be given nor put back.",
                amount, CharacterRecord.FormatRef(characterId));
            Finish(player, null, BankText.Unreadable, done: false);
            return;
        }

        Finish(player, restored, BankText.Failed, done: false);
    }

    // False once the window closed or was never opened. One that no longer holds is closed here.
    private bool TryFindOpen(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out OpenBank? open)
    {
        open = null;
        if (!online.TryGet(id, out player) || !_open.TryGetValue(player, out open))
            return false;

        if (CheckWindow(player, open.Spot) is not { } broken)
            return true;

        Close(player, broken);
        return false;
    }

    // Onto the window, or into the corner when it closed while the bank was writing. Balance: null when it is not known.
    private void Finish(OnlinePlayer player, long? balance, Phrase text, bool done)
    {
        if (!player.IsLive)
            return;

        if (!_open.TryGetValue(player, out var open))
        {
            chat.Notify(player, text);
            return;
        }

        if (balance is { } known)
            _open[player] = open with { Balance = known };

        Answer(player, text, done);
        SendState(player);
    }

    private void OnLeftWindow(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    // A banker the staff took away takes its windows with it here, within a second.
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
            return BankText.ClosedNotInWorld;

        if (!npcs.TryFind(spot, out var npc))
            return BankText.ClosedGone;

        return proximity.MeasureDistance(player.Id, new AreaId(spot.Area), npc.Position) is { } away && away <= TraderRules.WalkAway
            ? null
            : BankText.ClosedFar(TraderRules.WalkAwayMetres);
    }

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;

        rpc.SendBankClosed(player.Id);
        chat.Notify(player, why);
    }

    private void Answer(OnlinePlayer player, Phrase text, bool done) => rpc.SendBankAnswer(player.Id, chat.Translate(player, text), done);

    private void SendState(OnlinePlayer player)
    {
        wallet.TryCount(player.Id, out var gold);
        var open = _open[player] with { SeenGold = gold };
        _open[player] = open;
        rpc.SendBankState(player.Id, JsonSerializer.Serialize(new BankView(open.Balance, gold), BankJson.Default.BankView));
    }

    private sealed record MovementDetails(int Amount, long Balance);
}
