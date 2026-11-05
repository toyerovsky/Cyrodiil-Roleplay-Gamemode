using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Dice;

[Service]
internal sealed partial class DiceCommands(
    CommandRegistry commands,
    Rolls dice,
    DiceBoards boards,
    SelfMenu self,
    InteractionMenu interactions,
    RpConfig config)
{
    private const string BoardOption = "self.roll";
    private const string ContestOption = "player.contest";

    private const int BoardOrder = 20;
    private const int ContestOrder = 25;

    private const string ListSeparator = ", ";

    private void Start()
    {
        commands.Add(new CommandSpec { Name = "roll", Usage = DiceText.CmdRoll, Category = CommandCategory.Roleplay, Speaks = true }, Roll);
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

    // With the board open the dice land on it, and its throw is the roll: one roll, shown once.
    private void Roll(CommandCall call)
    {
        if (!RollNotation.TryParse(call.Arguments, out var groups, out var refusal))
        {
            call.Refuse(refusal);
            return;
        }

        if (boards.IsOpen(call.Sender))
        {
            boards.ThrowOn(call.Sender, call.Arguments.Trim().Length == 0 ? DicePick.AsInTray : DicePick.From(groups));
            return;
        }

        dice.Roll(call.Sender, groups);
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
