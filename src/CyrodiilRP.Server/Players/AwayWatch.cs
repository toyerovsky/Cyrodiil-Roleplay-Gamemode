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

// Who is away from the keyboard and who is writing in the chat, both shown by the name over the head (ShownNames). A
// player is away once they stood still, kept the camera where it was and wrote nothing for away.afterMinutes, or at
// once with /afk, and back as soon as they move, turn or start to write.
[Service]
internal sealed partial class AwayWatch(
    OnlinePlayers online,
    IEntities entities,
    RpcHandlers rpc,
    CommandRegistry commands,
    RpConfig config,
    SystemClock clock,
    Timers timers)
{
    private const string WatchJob = "players.away";

    // Coming back clears the mark within this.
    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(2);

    // How each player last stood, and when they last did anything.
    private readonly Dictionary<PlayerId, Stir> _stirs = [];

    private void Start()
    {
        timers.RunEvery(WatchJob, WatchEvery, Watch);
        rpc.TypingReported += OnTypingReported;
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

            if (!_stirs.TryGetValue(player.Id, out var last) || AwayRules.HasStirred(last.Stance, stance))
            {
                _stirs[player.Id] = new Stir(stance, now);
                player.IsAway = false;
                continue;
            }

            if (!player.IsAway && AwayRules.IsAway(last.AtMs, now, config.Away.After))
                player.IsAway = true;
        }
    }

    private void OnTypingReported(PlayerId id, bool typing)
    {
        if (!online.TryGet(id, out var player))
            return;

        player.IsTyping = typing;
        if (!typing)
            return;

        player.IsAway = false;
        if (_stirs.TryGetValue(player.Id, out var last))
            _stirs[player.Id] = last with { AtMs = clock.MonotonicMs };
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

        _stirs[player.Id] = new Stir(stance, clock.MonotonicMs - (long)config.Away.After.TotalMilliseconds);
        player.IsAway = true;
        call.Reply(ChatText.AwayNow);
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

    private sealed record Stir(Stance Stance, long AtMs);
}
