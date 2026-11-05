namespace CyrodiilRP.Server.Dice;

// Groups as asked, Dice in the order rolled. Reason: what a roll made for something else was for.
internal sealed record RollResult(IReadOnlyList<DiceGroup> Groups, IReadOnlyList<RolledDie> Dice, int Total, string? Reason, DateTimeOffset At);
