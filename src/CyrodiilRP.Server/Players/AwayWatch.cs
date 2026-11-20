using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Players;

// Who is away from the keyboard and who is writing in the chat, both shown by the name over the head (ShownNames), and
// the screen over the player's own game that says so. A player who stood still, kept the camera where it was and wrote
// nothing is first asked whether they are there (AwayRules.WarnFor before the mark), then away once away.afterMinutes
// are gone, or at once with /afk. They are back as soon as they move, turn, start to write or touch a window of the mode.
[Service]
internal sealed partial class AwayWatch(
    OnlinePlayers online,
    IEntities entities,
    RpcHandlers rpc,
    CommandRegistry commands,
    RpChat chat,
    RpConfig config,
    SystemClock clock,
    Timers timers)
{
    private const string WatchJob = "players.away";

    // The mark and the screen follow within this.
    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    // How each player last stood, and when they last did anything.
    private readonly Dictionary<PlayerId, Stir> _stirs = [];

    private void Start()
    {
        timers.RunEvery(WatchJob, WatchEvery, Watch);
        rpc.TypingReported += OnTypingReported;
        rpc.PresenceReported += OnPresenceReported;
        online.Left += player => _stirs.Remove(player.Id);
        commands.Add(new CommandSpec { Name = "afk", Usage = ChatText.CmdAfk, Category = CommandCategory.General, Aliases = ["away"] },
            GoAway);
    }

    private void Watch()
    {
        var now = clock.MonotonicMs;
        foreach (var player in online.All)
        {
            if (!player.IsPlaying || !TryRead(player, out var stance))
                continue;

            if (!_stirs.TryGetValue(player.Id, out var last))
                _stirs[player.Id] = new Stir(stance, now, AwayStage.Here, GraceUntilMs: now);
            else if (AwayRules.HasStirred(last.Stance, stance))
                Return(player, last with { Stance = stance }, now);
            else
                Advance(player, last, now);
        }
    }

    private void OnTypingReported(PlayerId id, bool typing)
    {
        if (!online.TryGet(id, out var player))
            return;

        player.IsTyping = typing;
        if (typing && _stirs.TryGetValue(player.Id, out var last))
            Return(player, last, clock.MonotonicMs);
    }

    private void OnPresenceReported(PlayerId id)
    {
        if (online.TryGet(id, out var player) && _stirs.TryGetValue(player.Id, out var last))
            Return(player, last, clock.MonotonicMs);
    }

    // Away from where they stand now, so the next look finds them still and keeps the mark.
    private void GoAway(CommandCall call)
    {
        var player = call.Sender;
        if (!player.IsPlaying || !TryRead(player, out var stance))
        {
            call.Refuse(ChatText.NeedCharacter);
            return;
        }

        var now = clock.MonotonicMs;
        var stilledAt = now - (long)config.Away.After.TotalMilliseconds;
        _stirs[player.Id] = new Stir(stance, stilledAt, AwayStage.Away, GraceUntilMs: now + (long)AwayRules.ManualGrace.TotalMilliseconds);
        player.IsAway = true;
        rpc.SendAwayMarked(player.Id, 0);
        call.Reply(ChatText.AwayNow);
    }

    // A sign of the player: the stage starts again, and whatever the screen showed goes.
    private void Return(OnlinePlayer player, Stir last, long now)
    {
        if (last.Stage == AwayStage.Away && now < last.GraceUntilMs)
        {
            _stirs[player.Id] = last;
            return;
        }

        _stirs[player.Id] = last with { AtMs = now, Stage = AwayStage.Here };
        Action end = last.Stage switch
        {
            AwayStage.Here => Nothing,
            AwayStage.Warned => () => rpc.SendAwayEnded(player.Id),
            AwayStage.Away => () =>
            {
                player.IsAway = false;
                rpc.SendAwayEnded(player.Id);
                chat.Notify(player, ChatText.AwayBack);
            },
        };
        end();
    }

    private void Advance(OnlinePlayer player, Stir last, long now)
    {
        var stage = AwayRules.StageAfter(last.AtMs, now, config.Away.After);
        if (stage == last.Stage)
            return;

        _stirs[player.Id] = last with { Stage = stage };
        Action show = stage switch
        {
            AwayStage.Here => Nothing,
            AwayStage.Warned => () => rpc.SendAwayWarning(player.Id, AwayRules.SecondsLeft(last.AtMs, now, config.Away.After)),
            AwayStage.Away => () =>
            {
                player.IsAway = true;
                rpc.SendAwayMarked(player.Id, AwayRules.SecondsSince(last.AtMs, now));
            },
        };
        show();
    }

    private bool TryRead(OnlinePlayer player, out Stance stance)
    {
        if (!entities.TryLookup(player.Id, out MainCharacter main))
        {
            stance = default;
            return false;
        }

        stance = new Stance(main.Position, main.As<AnimationMovement>().ControlRotation);
        return true;
    }

    private static void Nothing()
    {
    }

    // AtMs: the last sign of the player. GraceUntilMs: after /afk, a sign before this is the hand leaving the keys.
    private sealed record Stir(Stance Stance, long AtMs, AwayStage Stage, long GraceUntilMs);
}
