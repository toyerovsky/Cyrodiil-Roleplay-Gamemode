using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Statuses;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Dice;

[Service]
internal sealed partial class DiceCommands(
    CommandRegistry commands,
    Rolls dice,
    DiceBoards boards,
    SelfMenu self,
    InteractionMenu interactions,
    IEntities entities,
    RpConfig config)
{
    private const string BoardOption = "self.roll";
    private const string ContestOption = "player.contest";

    private const int BoardOrder = 20;
    private const int ContestOrder = 25;

    private const string ListSeparator = ", ";

    private const string AdvantageWord = "adv";
    private const string DisadvantageWord = "dis";

    private const int TavernDiceCount = 3;

    // /roll3: three d6, every one shown, for tavern games.
    private static readonly IReadOnlyList<DiceGroup> TavernDice = [new DiceGroup(DieKind.D6, TavernDiceCount)];

    private void Start()
    {
        commands.Add(new CommandSpec { Name = "roll", Usage = DiceText.CmdRoll, Category = CommandCategory.Roleplay, Speaks = true }, Roll);
        commands.Add(new CommandSpec { Name = "roll2", Usage = DiceText.CmdRollTwo, Category = CommandCategory.Roleplay, Speaks = true }, RollTwo);
        commands.Add(new CommandSpec { Name = "roll3", Usage = DiceText.CmdRollThree, Category = CommandCategory.Roleplay, Speaks = true },
            call => RollDice(call, TavernDice, DicePick.From(TavernDice)));
        commands.Add(new CommandSpec { Name = "board", Usage = DiceText.CmdBoard, Category = CommandCategory.Roleplay, NeedsCharacter = true },
            call => boards.Toggle(call.Sender));
        commands.Add(new CommandSpec { Name = "dice", Usage = DiceText.CmdDice, Category = CommandCategory.Roleplay, NeedsCharacter = true }, PickSet);

        self.Add(new SelfOption { Id = BoardOption, Label = DiceText.OptionBoard, Icon = WheelIcons.Dice, Order = BoardOrder, Run = boards.Open });
        interactions.Add(new InteractionOption
        {
            Id = ContestOption,
            Label = DiceText.OptionContest,
            Icon = WheelIcons.Dice,
            Order = ContestOrder,
            Speaks = true,
            Reach = config.Chat.Say,
            Run = dice.Contest,
        });
    }

    // Dice as written, or else a skill or an attribute by name.
    private void Roll(CommandCall call)
    {
        if (RollNotation.TryParse(call.Arguments, out var groups, out var refusal))
        {
            RollDice(call, groups, call.Arguments.Trim().Length == 0 ? DicePick.AsInTray : DicePick.From(groups));
            return;
        }

        if (TryFindStat(call, call.Arguments, refusal, out var stat))
            RollStat(call, stat, SkillRollMode.Normal);
    }

    private void RollTwo(CommandCall call)
    {
        SkillRollMode? mode = call.Words.Count == 0 ? null : call.Words[0].ToLowerInvariant() switch
        {
            AdvantageWord => SkillRollMode.Advantage,
            DisadvantageWord => SkillRollMode.Disadvantage,
            _ => null,
        };
        var name = call.SkipWords(1);
        if (mode is not { } chosen || name.Trim().Length == 0)
        {
            call.Refuse(DiceText.UseRollTwo);
            return;
        }

        if (TryFindStat(call, name, DiceText.UnknownSkill(name.Trim()), out var stat))
            RollStat(call, stat, chosen);
    }

    // With the board open the dice land on it, and its throw is the roll: one roll, shown once.
    private void RollDice(CommandCall call, IReadOnlyList<DiceGroup> groups, DicePick pick)
    {
        if (boards.IsOpen(call.Sender))
        {
            boards.ThrowOn(call.Sender, pick);
            return;
        }

        dice.Roll(call.Sender, groups);
    }

    // The bonus comes from the stat's value in the game now, so the character has to be in the world.
    private void RollStat(CommandCall call, Stat stat, SkillRollMode mode)
    {
        if (!entities.TryLookup(call.Sender.Id, out MainCharacter main) || !main.TryGetSkillAttributeValue((byte)stat, out var value))
        {
            call.Refuse(DiceText.SkillUnread);
            return;
        }

        dice.RollStat(call.Sender, stat, (int)MathF.Round(value), mode);
    }

    // NotNamed: the refusal when the text names no stat at all.
    private static bool TryFindStat(CommandCall call, string text, Phrase notNamed, out Stat stat)
    {
        var found = SkillRolls.Find(text);
        if (found.Count == 1)
        {
            stat = found[0];
            return true;
        }

        stat = default;
        call.Refuse(found.Count == 0
            ? notNamed
            : DiceText.SkillAmbiguous(text.Trim(), string.Join(ListSeparator, found.Select(each => each.DisplayName()))));
        return false;
    }

    private void PickSet(CommandCall call)
    {
        var sets = string.Join(ListSeparator, DiceSets.All);
        if (call.Words.Count == 0)
        {
            call.Reply(DiceText.YourSet(dice.FindSet(call.Sender), sets));
            return;
        }

        var set = call.Words[0].ToLowerInvariant();
        if (!dice.TryChooseSet(call.Sender, set))
        {
            call.Refuse(DiceText.UnknownSet(call.Words[0], sets));
            return;
        }

        call.Reply(DiceText.SetChosen(set));
    }
}
