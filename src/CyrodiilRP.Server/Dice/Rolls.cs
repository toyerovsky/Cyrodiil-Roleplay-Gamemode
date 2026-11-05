using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Dice;

// The one place that rolls: the server picks every number, the chat shows it to those around, the log keeps it.
[Service]
internal sealed partial class Rolls(
    RpChat chat,
    RpConfig config,
    OnlinePlayers online,
    Proximity proximity,
    DiceLog log,
    Fire fire,
    SystemClock clock)
{
    // The last rolls the board and the menu show.
    public const int RecentCount = 10;

    private const string LoadJob = "dice.load";

    private readonly Dictionary<long, string> _sets = [];

    private readonly Dictionary<long, LinkedList<DiceRollView>> _recent = [];

    public RollResult Roll(OnlinePlayer roller, IReadOnlyList<DiceGroup> groups, Phrase? reason = null)
    {
        var result = DiceRoller.Roll(groups, reason is null ? null : chat.Translate(roller, reason), clock.UtcNow, DiceRoller.RollDie);
        var asked = DiceFormat.FormatAsked(result.Groups);
        var dice = DiceFormat.FormatDice(result.Dice);
        var line = reason is null
            ? DiceText.Rolled(new ShownName(roller), asked, dice, result.Total)
            : DiceText.RolledFor(new ShownName(roller), asked, reason, dice, result.Total);
        chat.SendAround(roller, config.Chat.Emote, ChatLineKind.Attempt, line);
        Keep(roller, result);
        return result;
    }

    // A d20 each, the one asked wins a draw.
    public void Contest(OnlinePlayer actor, OnlinePlayer target)
    {
        var reason = chat.Translate(actor, DiceText.OptionContest);
        var rolled = DiceRoller.Roll(RollNotation.Default, reason, clock.UtcNow, DiceRoller.RollDie);
        var against = DiceRoller.Roll(RollNotation.Default, reason, clock.UtcNow, DiceRoller.RollDie);
        var winner = rolled.Total > against.Total ? actor : target;
        chat.SendAround(actor, config.Chat.Emote, ChatLineKind.Attempt,
            DiceText.Contest(new ShownName(actor), new ShownName(target), rolled.Total, against.Total, new ShownName(winner)));
        Keep(actor, rolled);
        Keep(target, against);
    }

    public string FindSet(OnlinePlayer player) =>
        player.Character is { } character && _sets.TryGetValue(character.Id, out var set) ? set : DiceSets.Default;

    public bool TryChooseSet(OnlinePlayer player, string set)
    {
        if (!DiceSets.IsKnown(set) || player.Character is not { } character)
            return false;

        _sets[character.Id] = set;
        log.SaveSet(character.Id, set);
        return true;
    }

    public DiceView BuildView(OnlinePlayer player)
    {
        IReadOnlyList<DiceRollView> recent = player.Character is { } character && _recent.TryGetValue(character.Id, out var rolls)
            ? rolls.ToList()
            : [];
        return new DiceView(FindSet(player), DiceSets.All, recent, RollNotation.MaxDice);
    }

    private void Start()
    {
        online.Loaded += OnLoaded;
        online.Left += OnLeft;
    }

    private void Keep(OnlinePlayer player, RollResult result)
    {
        if (player.Character is not { } character)
            return;

        string? area = proximity.TryLocate(player.Id, out var located, out _) ? located.ToString() : null;
        log.Write(character.Id, player.Name, result, area);
        Remember(character.Id, new DiceRollView(result.At.ToUnixTimeMilliseconds(), DiceFormat.FormatAsked(result.Groups),
            result.Dice.Select(die => new DieView((int)die.Kind, die.Value)).ToList(), result.Total, result.Reason));
    }

    private void Remember(long characterId, DiceRollView roll)
    {
        if (!_recent.TryGetValue(characterId, out var rolls))
            _recent[characterId] = rolls = new LinkedList<DiceRollView>();

        rolls.AddFirst(roll);
        while (rolls.Count > RecentCount)
            rolls.RemoveLast();
    }

    // The set and the last rolls of the character, which a roll made while they load goes on top of.
    private void OnLoaded(OnlinePlayer player)
    {
        if (player.Character is not { } character)
            return;

        fire.Run(LoadJob, async () =>
        {
            var record = await log.LoadAsync(character.Id, RecentCount);
            if (!player.IsLive)
                return;

            if (DiceSets.IsKnown(record.Set) && !_sets.ContainsKey(character.Id))
                _sets[character.Id] = record.Set!;

            foreach (var roll in record.Recent)
                RememberOlder(character.Id, DiceLog.BuildView(roll));
        });
    }

    // Newest first, under what is already kept: a roll made while the record loaded is newer than any in it.
    private void RememberOlder(long characterId, DiceRollView roll)
    {
        if (!_recent.TryGetValue(characterId, out var rolls))
            _recent[characterId] = rolls = new LinkedList<DiceRollView>();

        if (rolls.Count < RecentCount && (rolls.Last is not { } oldest || roll.At < oldest.Value.At))
            rolls.AddLast(roll);
    }

    private void OnLeft(OnlinePlayer player)
    {
        if (player.Character is not { } character)
            return;

        _sets.Remove(character.Id);
        _recent.Remove(character.Id);
    }
}
