using System.Text.Json;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.PlayerMenu;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Dice;

// The players' dice boards: which are open, what they throw, and what lands on them. The board only says how many of
// each die: the numbers are the server's.
[Service]
internal sealed partial class DiceBoards(
    RpcHandlers rpc,
    OnlinePlayers online,
    Rolls dice,
    CommandRegistry commands,
    PlayerMenus menus,
    RpChat chat,
    SystemClock clock,
    ILogger logger)
{
    // About as long as a throw takes to settle on the board.
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(1.5);

    private readonly HashSet<PlayerId> _open = [];

    private readonly Dictionary<PlayerId, long> _lastThrowMs = [];

    public bool IsOpen(OnlinePlayer player) => _open.Contains(player.Id);

    // Only with a character in play: before it, the welcome or the name form hold the screen.
    public void Open(OnlinePlayer player)
    {
        if (!player.IsLoaded || player.Character is null || !player.IsPlaying)
        {
            chat.Notify(player, ChatText.NeedCharacter);
            return;
        }

        _open.Add(player.Id);
        SendState(player);
    }

    public void Close(OnlinePlayer player)
    {
        if (_open.Remove(player.Id))
            rpc.SendCloseDiceBoard(player.Id);
    }

    public void Toggle(OnlinePlayer player)
    {
        if (IsOpen(player))
            Close(player);
        else
            Open(player);
    }

    // /roll with the board open: the board throws them, and its throw comes back here as any other.
    public void ThrowOn(OnlinePlayer player, DicePick pick) => rpc.SendThrowOnBoard(player.Id, pick.D6, pick.D10, pick.D20, pick.D100);

    // A roll already made for something else, shown landing on its numbers.
    public void Present(OnlinePlayer player, DicePick pick, RollResult result, Phrase reason) =>
        rpc.SendPresentDice(player.Id, chat.Translate(player, reason), pick.D6, pick.D10, pick.D20, pick.D100,
            result.Dice.Select(die => die.Value).ToArray());

    private void Start()
    {
        rpc.DiceAsked += id => WithPlayer(id, Open);
        rpc.DiceLeft += id => _open.Remove(id);
        rpc.DiceThrowAsked += OnThrow;
        rpc.DiceSetChosen += OnSetChosen;
        online.Left += player =>
        {
            _open.Remove(player.Id);
            _lastThrowMs.Remove(player.Id);
        };
    }

    private void WithPlayer(PlayerId id, Action<OnlinePlayer> act)
    {
        if (online.TryGet(id, out var player) && player.IsLoaded)
            act(player);
    }

    private void OnThrow(PlayerId id, DiceThrow asked)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded || player.Character is null)
            return;

        if (!asked.Pick.IsValid)
        {
            Refuse(player, asked, DiceText.Pick(RollNotation.MaxDice));
            return;
        }

        if (commands.FindSpeechRefusal(player) is { } muted)
        {
            Refuse(player, asked, muted);
            return;
        }

        var now = clock.MonotonicMs;
        if (_lastThrowMs.TryGetValue(id, out var last) && now - last < Cooldown.TotalMilliseconds)
        {
            Refuse(player, asked, DiceText.TooSoon);
            return;
        }

        _lastThrowMs[id] = now;
        var result = dice.Roll(player, asked.Pick.ToGroups());
        rpc.SendDiceThrown(id, asked.RequestId, result.Dice.Select(die => die.Value).ToArray());
        if (IsOpen(player))
            SendState(player);
    }

    private void OnSetChosen(PlayerId id, string set, bool fromMenu)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded)
            return;

        if (!dice.TryChooseSet(player, set))
        {
            logger.LogWarning("[CyrodiilRP] {Player} asked for the dice set {Set}, which is none.", id, set);
            return;
        }

        if (fromMenu)
            menus.Show(player, MenuTabs.Dice);
        else if (IsOpen(player))
            SendState(player);
    }

    private void Refuse(OnlinePlayer player, DiceThrow asked, Phrase why) =>
        rpc.SendDiceRefused(player.Id, asked.RequestId, chat.Translate(player, why));

    private void SendState(OnlinePlayer player) =>
        rpc.SendDiceState(player.Id, JsonSerializer.Serialize(dice.BuildView(player), DiceJson.Default.DiceView));
}
