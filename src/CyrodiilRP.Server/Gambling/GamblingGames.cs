using System.Text.Json;
using CyrodiilRP.Common.Gambling;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Gambling;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Dice;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Sanctions;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Gambling;

[Service]
internal sealed partial class GamblingGames(
    GamblingTables tables, RpConfig config, Persistence persistence, Wallet wallet, GoldDeliveries deliveries,
    Rolls rolls, Asks asks, OnlinePlayers online, CommandRegistry commands, Freezes freezes,
    RpcHandlers rpc, RpChat chat, ShownNames names, GamblingLog log, Audit audit, Fire fire, Timers timers, SystemClock clock, ILogger logger)
{
    private static readonly TimeSpan WatchEvery = TimeSpan.FromMilliseconds(GamblingPicks.RelayMs);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);
    private const string InviteJob = "gambling.invite";
    private readonly GamblingSessionRegistry _registry = new();
    private readonly AskPacing _pacing = new(InviteJob, timers, clock);
    private readonly HashSet<OnlinePlayer> _open = [];
    private bool _ready;
    public IReadOnlyList<GamblingSession> Running => _registry.Sessions;
    public bool HasGame(OnlinePlayer player) => _registry.Find(player) is not null;
    public bool TableBusy(long id) => _registry.Count(id) > 0;
    private GamblingSettings Settings => config.Gambling;

    private void Start()
    {
        fire.Run("gambling.startup", LoadAsync);
        timers.RunEvery("gambling.watch", WatchEvery, Watch);
        rpc.GamblingOpened += OnOpen;
        rpc.GamblingHidden += id => { if (online.TryGet(id, out var player)) _open.Remove(player); };
        rpc.GamblingInvited += OnInvite;
        rpc.GamblingActed += OnAct;
        online.Left += player =>
        {
            _open.Remove(player);
            _pacing.Forget(player);
        };
    }

    private async Task LoadAsync()
    {
        try
        {
            var refunded = await persistence.Writer.EnqueueAsync("gambling.recover", (db, ct) => GamblingBook.RefundOpenAsync(db, clock.UtcNow, ct));
            foreach (var game in refunded)
                audit.Log("gamble.refund", Audit.ServerRef, null, new GameDetails(game.Id, game.TableId, game.Pot, game.WinnerCharacterId, game.Cut));

            await tables.LoadAsync();
            _ready = true;
            foreach (var player in online.All.Where(player => player.IsLoaded))
            {
                if (player.IsPlaying) deliveries.Deliver(player);
                log.Refresh(player);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] Gambling could not recover open games and tables, retrying.");
            timers.RunAfter("gambling.startup", RetryAfter, () => fire.Run("gambling.startup", LoadAsync));
        }
    }

    // What a decision in a running match needs. A mute or a freeze stops only a new game: see CheckPlayer.
    private Phrase? CheckPresence(OnlinePlayer player)
    {
        if (!_ready)
            return GamblingText.Loading;
        if (!player.IsLive || !player.IsLoaded || !player.IsPlaying || player.Character is null || !wallet.IsInWorld(player.Id))
            return GamblingText.InWorld;
        return null;
    }

    public Phrase? CheckPlayer(OnlinePlayer player)
    {
        if (CheckPresence(player) is { } away)
            return away;
        if (freezes.IsFrozen(player))
            return GamblingText.Frozen;
        return commands.FindSpeechRefusal(player);
    }

    public Phrase? CheckInvite(OnlinePlayer actor, OnlinePlayer target)
    {
        if (CheckPlayer(actor) is { } refused)
            return refused;
        if (_pacing.CountWaitSeconds(actor) is > 0 and var wait)
            return GamblingText.Cooldown(wait);
        if (actor == target)
            return CommandsText.NotYourself;
        if (CheckPlayer(target) is not null)
            return GamblingText.Unavailable;
        if (_registry.Find(actor) is not null || _registry.Find(target) is not null)
            return GamblingText.Busy;
        if (tables.Shared(actor, target) is not { } table)
            return GamblingText.SameTable;
        return _registry.Count(table.Id) >= Settings.GamesPerTable ? GamblingText.TableFull : null;
    }

    public void Open(OnlinePlayer player, OnlinePlayer? opponent, bool rules)
    {
        if (!player.IsLoaded || player.Character is null || !player.IsPlaying)
            return;

        _open.Add(player);
        Send(player, opponent?.Number, rules);
    }

    public Phrase? Invite(OnlinePlayer actor, OnlinePlayer target, int stake, GamblingMode mode)
    {
        if (CheckInvite(actor, target) is { } refusal)
            return refusal;
        if (stake < Settings.MinStake || stake > Settings.MaxStake)
            return GamblingText.StakeRange(Settings.MinStake, Settings.MaxStake);
        if (!wallet.TryCount(actor.Id, out var gold) || gold < stake)
            return GamblingText.Gold(stake);

        var invite = new PlayerAsk(actor, target);
        var session = new GamblingSession(tables.Shared(actor, target)!, invite,
            new GamblingMatch(mode, stake, Settings.PokerRoundsToWin, Settings.MaxReplays), clock.MonotonicMs + Settings.InviteSeconds * 1000L);
        _registry.Add(session);
        _pacing.StartCooldown(actor);
        asks.Open(invite, AskKinds.Gambling, new AskAnswers(_ => Accept(session), _ => Withdraw(session)),
            TimeSpan.FromSeconds(Settings.InviteSeconds), GamblingText.Invitation(GamblingWords.Mode(mode), stake));
        foreach (var player in new[] { actor, target })
        {
            _open.Remove(player);
            rpc.SendGamblingState(player.Id, (int)GamblingMessageKind.Invitation, string.Empty);
        }
        return null;
    }

    private void Accept(GamblingSession session)
    {
        if (session.Stage != GamblingStage.Invited)
            return;

        var first = session.Player(GamblingMatch.First);
        var second = session.Player(GamblingMatch.Second);
        if (clock.MonotonicMs >= session.ExpiresAt || CheckPlayer(first) is not null || CheckPlayer(second) is not null
            || !tables.IsAt(first, session.Table) || !tables.IsAt(second, session.Table))
        {
            Refuse(second, GamblingText.Unavailable);
            Withdraw(session);
            return;
        }

        var stake = session.Match.Stake;
        var escrow = GamblingEscrow.TakeStakes(stake,
            (side, amount) => wallet.TryTake(session.Player(side).Id, amount),
            (_, amount) => ReturnGold(first, session.FirstCharacterId, amount));
        var refusal = escrow switch
        {
            EscrowOutcome.Accepted => (Phrase?)null,
            EscrowOutcome.FirstCannotPay => GamblingText.Gold(stake),
            EscrowOutcome.SecondCannotPay => GamblingText.Gold(stake),
        };
        if (refusal is not null)
        {
            Refuse(escrow == EscrowOutcome.FirstCannotPay ? first : second, refusal);
            Refuse(escrow == EscrowOutcome.FirstCannotPay ? second : first, GamblingText.Unavailable);
            Withdraw(session);
            return;
        }

        asks.Close(session.Invite);
        session.Stage = GamblingStage.Starting;
        session.StartedAt = clock.UtcNow;
        _open.Add(first);
        _open.Add(second);
        SendOpen(session);
        PersistStart(session);
    }

    private void ReturnGold(OnlinePlayer player, long characterId, int amount)
    {
        if (player.IsLive && wallet.TryGive(player.Id, amount))
            return;

        logger.LogError("[CyrodiilRP] Gambling could not immediately return {Amount} gold to character {Character}, owing it.", amount, characterId);
        fire.Run("gambling.rollback", async () =>
        {
            await persistence.Writer.EnqueueAsync("gambling.rollback", async (db, ct) =>
            {
                db.GoldDeliveries.Add(new GoldDelivery { CharacterId = characterId, Amount = amount, CreatedAt = clock.UtcNow, Reason = GoldDeliveryReason.Gambling });
                await db.SaveChangesAsync(ct);
            });
            if (player.IsLive) deliveries.Deliver(player);
        });
    }

    private void PersistStart(GamblingSession session) => fire.Run("gambling.create", async () =>
    {
        try
        {
            var row = Snapshot(session);
            session.Id = await persistence.Writer.EnqueueAsync("gambling.create", (db, ct) => GamblingBook.StartAsync(db, row, ct));
            session.Stage = GamblingStage.Playing;
            audit.Log("gamble.start", session.Invite.Asker.Ref, session.Invite.Asked.Ref, new GameDetails(session.Id, session.Table.Id, session.Match.Pot, null, 0));
            Drive(session);
        }
        catch (Exception exception)
        {
            Retry(session, exception, () => PersistStart(session));
        }
    });

    private void OnOpen(PlayerId id, int opponent, bool rules)
    {
        if (online.TryGet(id, out var player))
            Open(player, opponent >= 0 && opponent <= ushort.MaxValue && online.TryFind((ushort)opponent, out var target) ? target : null, rules);
    }

    private void OnInvite(PlayerId id, GamblingInvitation invitation)
    {
        var opponent = invitation.Opponent;
        var stake = invitation.Stake;
        var typedMode = invitation.Mode;
        if (!online.TryGet(id, out var player))
            return;
        if (!Enum.TryParse<GamblingMode>(typedMode, true, out var mode) || !Enum.IsDefined(mode))
        {
            logger.LogWarning("[CyrodiilRP] Gambling received unknown mode {Mode} from {Player}.", typedMode, id);
            Refuse(player, GamblingText.CmdGamble);
            return;
        }
        if (opponent < 0 || opponent > ushort.MaxValue || !online.TryFind((ushort)opponent, out var target))
        {
            Refuse(player, GamblingText.Unavailable);
            return;
        }

        if (Invite(player, target, stake, mode) is { } refusal) Refuse(player, refusal);
    }

    private void OnAct(PlayerId id, GamblingDecision decision)
    {
        var game = decision.Game;
        var version = decision.Version;
        var typedAction = decision.Action;
        var amount = decision.Amount;
        if (!online.TryGet(id, out var player) || _registry.Find(game) is not { } session || !session.Contains(player))
            return;
        if (!Enum.TryParse<GamblingAction>(typedAction, true, out var action) || !Enum.IsDefined(action))
        {
            logger.LogWarning("[CyrodiilRP] Gambling received unknown action {Action} from {Player}.", typedAction, id);
            Refuse(player, GamblingText.Decision);
            return;
        }
        if (CheckPresence(player) is { } refusal)
        {
            Refuse(player, refusal);
            return;
        }
        MarkGone(session);
        var side = session.Side(player);
        var match = session.Match;
        if (session.Stage != GamblingStage.Playing || version != session.Version || clock.MonotonicMs >= session.ExpiresAt
            || session.Throws.Active || session.FirstGone || session.SecondGone || !match.Allows(side, action))
        {
            Refuse(player, GamblingText.Decision);
            return;
        }

        var oldPhase = match.Phase;
        switch (action)
        {
            case GamblingAction.Pass:
                match.Pass(side);
                break;
            case GamblingAction.Raise:
                if (amount < 1 || amount > RaiseLimit(session, side))
                {
                    Refuse(player, GamblingText.RaiseLimit(RaiseLimit(session, side)));
                    return;
                }
                if (!wallet.TryTake(player.Id, amount))
                {
                    Refuse(player, GamblingText.Gold(amount));
                    return;
                }
                match.Raise(side, amount);
                AuditGame("gamble.raise", session, player.Ref);
                break;
            case GamblingAction.Call:
                if (!wallet.TryTake(player.Id, match.PendingRaise))
                {
                    Refuse(player, GamblingText.Gold(match.PendingRaise));
                    return;
                }
                match.Call(side);
                AuditGame("gamble.call", session, player.Ref);
                break;
            case GamblingAction.Fold:
                match.Fold(side);
                AuditGame("gamble.fold", session, player.Ref);
                break;
            case GamblingAction.Pick:
                if (amount < 0 || amount > GamblingMatch.AllDiceMask)
                {
                    Refuse(player, GamblingText.Decision);
                    return;
                }
                match.Pick(side, amount);
                session.Picks.Select(side, amount);
                break;
            case GamblingAction.Select:
                if (amount < 0 || amount > GamblingMatch.AllDiceMask)
                {
                    Refuse(player, GamblingText.Decision);
                    return;
                }
                session.Picks.Select(side, amount);
                if (session.Picks.Flush(clock.MonotonicMs)) SendOpen(session);
                return;
        }

        if (action == GamblingAction.Pick && !match.BothPicked)
        {
            SendOpen(session);
            return;
        }

        session.Version++;
        if (oldPhase != match.Phase || action == GamblingAction.Pass)
            SetDeadline(session);
        Drive(session);
    }

    private void Drive(GamblingSession session)
    {
        MarkGone(session);
        if (session.CancellationRequested)
        {
            session.Throws.Stop();
            session.Match.Cancel();
            session.EndStatus = GamblingStatus.Cancelled;
            Finish(session);
            return;
        }
        if (session.FirstGone || session.SecondGone)
        {
            session.Throws.Stop();
            session.Match.Forfeit(session.FirstGone, session.SecondGone);
        }

        var match = session.Match;
        if (match.Phase == GamblingPhase.Rolling || match.Phase == GamblingPhase.Reroll && match.BothPicked)
        {
            var kind = match.Phase == GamblingPhase.Rolling ? GamblingThrowKind.Initial : GamblingThrowKind.Reroll;
            if (kind == GamblingThrowKind.Initial) session.Picks.Clear();
            while (session.ThrowSeat <= GamblingMatch.Second)
            {
                var seat = session.ThrowSeat++;
                Action roll = kind switch
                {
                    GamblingThrowKind.Initial => () => match.RollSeat(seat, count => Roll(session, seat, count, kind)),
                    GamblingThrowKind.Reroll => () => match.RerollSeat(seat, count => Roll(session, seat, count, kind)),
                };
                roll();
                session.Picks.Select(seat, 0);
                session.Version++;
                if (session.Throws.Start(clock.MonotonicMs))
                {
                    Save(session);
                    return;
                }
            }
            session.ThrowSeat = GamblingMatch.First;
            session.Picks.Clear();
            SetDeadline(session);
        }

        if (match.Phase == GamblingPhase.Finished)
        {
            session.EndStatus = match.Winner == GamblingMatch.NoWinner ? GamblingStatus.Refunded : GamblingStatus.Settled;
            Finish(session);
            return;
        }

        Save(session);
    }

    private int[] Roll(GamblingSession session, int side, int count, GamblingThrowKind kind)
    {
        var player = session.Player(side);
        var reason = GamblingText.Reason(session.Id);
        var pick = new DicePick(count, 0, 0, 0);
        var result = rolls.Roll(player, pick.ToGroups(), reason);
        var values = result.Dice.Select(die => die.Value).ToArray();
        var indices = kind switch
        {
            GamblingThrowKind.Initial => Enumerable.Range(0, count).ToArray(),
            GamblingThrowKind.Reroll => Enumerable.Range(0, PokerHand.DiceCount)
                .Where(index => (session.Match.Picked(side) & (1 << index)) != 0).ToArray(),
        };
        session.Throws.Add(session.Id, side, session.Match.Round, session.Match.Replay, kind, indices, values, rolls.FindSet(player));
        return values;
    }

    private void Save(GamblingSession session)
    {
        session.Stage = GamblingStage.Saving;
        session.SavingSince = clock.MonotonicMs;
        var row = Snapshot(session);
        SendOpen(session);
        PersistSave(session, row);
    }

    private void PersistSave(GamblingSession session, GamblingGame row) => fire.Run("gambling.save", async () =>
    {
        try
        {
            var saved = await persistence.Writer.EnqueueAsync("gambling.save", (db, ct) => GamblingBook.SaveAsync(db, row, ct));
            if (!saved) throw new InvalidOperationException($"Open gambling game #{row.Id} disappeared during a decision.");
            session.Stage = GamblingStage.Playing;
            session.ExpiresAt = GamblingTiming.Resume(session.ExpiresAt, session.SavingSince, clock.MonotonicMs);
            MarkGone(session);
            if (session.CancellationRequested || session.FirstGone || session.SecondGone)
                Drive(session);
            else
                SendOpen(session);
        }
        catch (Exception exception)
        {
            Retry(session, exception, () => PersistSave(session, row));
        }
    });

    public Task<bool> CancelAsync(long id, string actor)
    {
        if (_registry.Find(id) is not { } session || session.Stage is GamblingStage.Invited or GamblingStage.Closed)
            return Task.FromResult(false);
        if (session.Stage == GamblingStage.Finishing)
            return session.CancellationRequested ? session.Completion.Task : Task.FromResult(false);

        session.CancellationRequested = true;
        session.EndActor = actor;
        if (session.Stage == GamblingStage.Playing) Drive(session);
        return session.Completion.Task;
    }

    private void Finish(GamblingSession session)
    {
        session.Stage = GamblingStage.Finishing;
        SendOpen(session);
        PersistFinish(session, Snapshot(session));
    }

    private void PersistFinish(GamblingSession session, GamblingGame row) => fire.Run("gambling.finish", async () =>
    {
        try
        {
            await persistence.Writer.EnqueueAsync("gambling.finish", (db, ct) => GamblingBook.FinishAsync(db, row, ct));
            var action = row.Status switch
            {
                GamblingStatus.Settled => "gamble.settle",
                GamblingStatus.Refunded => "gamble.refund",
                GamblingStatus.Cancelled => "gamble.cancel",
                GamblingStatus.Open => throw new InvalidOperationException("Cannot finish an open game."),
            };
            AuditGame(action, session, session.EndActor);
            session.Stage = GamblingStage.Closed;
            SendOpen(session);
            _registry.Remove(session);
            session.Completion.TrySetResult(session.EndStatus == GamblingStatus.Cancelled);
            foreach (var player in new[] { session.Invite.Asker, session.Invite.Asked }.Where(player => player.IsLive))
            {
                deliveries.Deliver(player);
                log.Refresh(player);
            }
        }
        catch (Exception exception)
        {
            Retry(session, exception, () => PersistFinish(session, row));
        }
    });

    private GamblingGame Snapshot(GamblingSession session)
    {
        var match = session.Match;
        var winner = session.EndStatus == GamblingStatus.Settled ? match.Winner : GamblingMatch.NoWinner;
        var now = clock.UtcNow;
        return new GamblingGame
        {
            Id = session.Id, OperationId = session.OperationId, Mode = match.Mode, TableId = session.Table.Id,
            FirstCharacterId = session.FirstCharacterId, SecondCharacterId = session.SecondCharacterId,
            FirstName = session.FirstName, SecondName = session.SecondName, Stake = match.Stake,
            FirstRaises = match.Raises[GamblingMatch.First], SecondRaises = match.Raises[GamblingMatch.Second], Pot = match.Pot,
            Status = session.EndStatus, WinnerCharacterId = winner == GamblingMatch.NoWinner ? null : winner == GamblingMatch.First ? session.FirstCharacterId : session.SecondCharacterId,
            Cut = winner == GamblingMatch.NoWinner ? 0 : GamblingEscrow.Cut(match.Pot, Settings.CutPercent),
            StartedAt = session.StartedAt, UpdatedAt = now, EndedAt = session.EndStatus == GamblingStatus.Open ? null : now,
            Rounds = JsonSerializer.Serialize(new GamblingJournal(match.Rounds, match.Round, match.Replay, match.Phase,
                match.Hand(GamblingMatch.First), match.Hand(GamblingMatch.Second), match.Wins.ToArray(), match.Turn, match.PendingRaise), GamblingJson.Default.GamblingJournal),
        };
    }

    private void Watch()
    {
        foreach (var session in _registry.Sessions.ToArray())
        {
            MarkGone(session);
            CancelForFrozen(session);
            Action act = session.Stage switch
            {
                GamblingStage.Invited => () =>
                {
                    if (session.FirstGone || session.SecondGone || clock.MonotonicMs >= session.ExpiresAt) Withdraw(session);
                },
                GamblingStage.Playing => () =>
                {
                    if (session.FirstGone || session.SecondGone) Drive(session);
                    else if (session.Throws.Active)
                    {
                        if (session.Throws.Advance(clock.MonotonicMs))
                        {
                            if (session.Throws.Active) SendOpen(session);
                            else
                            {
                                session.Version++;
                                if (session.ThrowSeat > GamblingMatch.Second)
                                {
                                    session.ThrowSeat = GamblingMatch.First;
                                    session.Picks.Clear();
                                    SetDeadline(session);
                                }
                                Drive(session);
                            }
                        }
                    }
                    else if (clock.MonotonicMs >= session.ExpiresAt)
                    {
                        if (session.Match.Phase == GamblingPhase.Call) AuditGame("gamble.fold", session, session.Player(session.Match.Turn).Ref);
                        session.Match.Timeout();
                        session.Version++;
                        SetDeadline(session);
                        Drive(session);
                    }
                    else if (session.Picks.Flush(clock.MonotonicMs)) SendOpen(session);
                },
                GamblingStage.Starting => () => { },
                GamblingStage.Saving => () => { },
                GamblingStage.Finishing => () => { },
                GamblingStage.Closed => throw new InvalidOperationException("A closed gambling session is still registered."),
            };
            act();
        }
    }

    // A frozen player cannot reach the window, so their match would run out on its clock against them: it ends as a
    // cancelled one, with everyone's gold back. Starting, saving and playing all end through CancellationRequested.
    private void CancelForFrozen(GamblingSession session)
    {
        if (session.CancellationRequested || session.Stage is GamblingStage.Invited or GamblingStage.Finishing or GamblingStage.Closed
            || !freezes.IsFrozen(session.Invite.Asker) && !freezes.IsFrozen(session.Invite.Asked))
            return;

        session.CancellationRequested = true;
        if (session.Stage == GamblingStage.Playing) Drive(session);
    }

    private void MarkGone(GamblingSession session) => session.MarkGone(!tables.IsAt(session.Invite.Asker, session.Table), !tables.IsAt(session.Invite.Asked, session.Table));

    private void Withdraw(GamblingSession session)
    {
        if (session.Stage != GamblingStage.Invited)
            return;

        asks.Close(session.Invite);
        _registry.Remove(session);
        foreach (var player in new[] { session.Invite.Asker, session.Invite.Asked }.Where(player => player.IsLive))
        {
            chat.Notify(player, GamblingText.InvitationClosed);
            if (_open.Contains(player)) Send(player, null, false);
        }
    }

    private void SetDeadline(GamblingSession session) => session.ExpiresAt = GamblingTiming.Deadline(clock.MonotonicMs,
        session.Match.Phase, Settings.DecisionSeconds, session.Throws.Active);

    private int RaiseLimit(GamblingSession session, int side) => wallet.TryCount(session.Player(side).Id, out var mine)
        && wallet.TryCount(session.Player(GamblingMatch.Other(side)).Id, out var theirs) ? Math.Min(session.Match.Stake, Math.Min(mine, theirs)) : 0;

    private void SendOpen(GamblingSession session)
    {
        foreach (var player in new[] { session.Invite.Asker, session.Invite.Asked })
            if (player.IsLive && _open.Contains(player)) Send(player, null, false);
    }

    private void Send(OnlinePlayer player, int? opponent, bool rules)
    {
        var session = _registry.Find(player);
        var table = session?.Table ?? tables.Find(player);
        var opponents = table is null ? [] : online.All.Where(target => target != player && tables.IsAt(target, table))
            .Select(target => new GamblingOpponentView(target.Number, names.Shown(player, target), CheckInvite(player, target) is { } why ? chat.Translate(player, why) : null)).ToArray();
        GamblingMatchView? view = session is { Stage: not GamblingStage.Invited } && session.Contains(player) ? MatchView(session, player) : null;
        var state = new GamblingView(table?.Id, Settings.MinStake, Settings.MaxStake, Settings.CutPercent, Settings.PokerRoundsToWin,
            Settings.MaxReplays, Settings.InviteSeconds, Settings.DecisionSeconds, rules, opponents, opponent, view);
        rpc.SendGamblingState(player.Id, (int)GamblingMessageKind.State, JsonSerializer.Serialize(state, GamblingJson.Default.GamblingView));
    }

    private GamblingMatchView MatchView(GamblingSession session, OnlinePlayer reader)
    {
        var match = session.Match;
        var side = session.Side(reader);
        var other = GamblingMatch.Other(side);
        var mine = session.Throws.Hand(side, match.Round, match.Replay);
        var theirs = session.Throws.Hand(other, match.Round, match.Replay);
        string Rank(int[] hand) => hand.Length == PokerHand.DiceCount ? chat.Translate(reader, GamblingWords.Hand(PokerHand.Evaluate(hand).Rank)) : string.Empty;
        var rolling = session.Throws.Active;
        int ShownWins(int seat) => match.Wins[seat] - (rolling && (match.Phase is GamblingPhase.RoundResult or GamblingPhase.Finished)
            && match.LastWinner == seat ? 1 : 0);
        var busy = rolling || session.Stage != GamblingStage.Playing && session.Stage != GamblingStage.Closed;
        var winner = match.Winner == GamblingMatch.NoWinner ? GamblingMatch.NoWinner : match.Winner == side ? GamblingMatch.First : GamblingMatch.Second;
        var result = session.Stage == GamblingStage.Closed
            ? chat.Translate(reader, session.EndStatus == GamblingStatus.Cancelled ? GamblingText.Cancelled
                : winner == GamblingMatch.NoWinner ? GamblingText.Draw : winner == GamblingMatch.First ? GamblingText.Won : GamblingText.Lost)
            : null;
        return new GamblingMatchView(session.Id, session.Version, match.Mode, rolling ? GamblingPhase.Rolling : match.Phase, busy, match.Stake, match.Pot,
            match.Round, match.Replay, [ShownWins(side), ShownWins(other)], names.Shown(reader, reader), names.Shown(reader, session.Player(other)),
            mine, theirs, rolling ? string.Empty : Rank(mine), rolling ? string.Empty : Rank(theirs), !rolling && match.Turn == side,
            match.Confirmed(side), RaiseLimit(session, side), match.PendingRaise,
            rolling ? 0 : Math.Max(0, session.ExpiresAt - clock.MonotonicMs), rolling ? GamblingMatch.NoWinner : winner, result,
            side, rolling ? session.Throws.Current!.Seat : match.Turn, session.Picks.Masks, session.Throws.Published,
            session.Throws.Current?.Id, session.Throws.Elapsed(clock.MonotonicMs), GamblingThrows.AnimationMs,
            rolling ? GamblingMatch.NoWinner : match.LastWinner);
    }

    private void Refuse(OnlinePlayer player, Phrase phrase)
    {
        if (_open.Contains(player)) rpc.SendGamblingState(player.Id, (int)GamblingMessageKind.Refused, chat.Translate(player, phrase));
        else chat.Notify(player, phrase);
    }

    private void Retry(GamblingSession session, Exception exception, Action retry)
    {
        logger.LogError(exception, "[CyrodiilRP] Gambling game #{Game} could not be persisted, escrow remains held for retry.", session.Id);
        timers.RunAfter("gambling.retry", RetryAfter, retry);
    }

    private void AuditGame(string action, GamblingSession session, string actor) => audit.Log(action, actor, null,
        new GameDetails(session.Id, session.Table.Id, session.Match.Pot,
            session.Match.Winner == GamblingMatch.NoWinner ? null : session.Match.Winner == GamblingMatch.First ? session.FirstCharacterId : session.SecondCharacterId,
            session.Match.Winner == GamblingMatch.NoWinner ? 0 : GamblingEscrow.Cut(session.Match.Pot, Settings.CutPercent)));

    private sealed record GameDetails(long Game, long Table, int Pot, long? Winner, int Cut);
}
