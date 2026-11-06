namespace CyrodiilRP.Server.Dice;

// At in Unix milliseconds. Asked as typed in the chat line ("1d20 + 2d6"), the dice in the order rolled. Reason: what a
// roll made for something else was for, otherwise null.
internal sealed record DiceRollView(long At, string Asked, IReadOnlyList<DieView> Dice, int Total, string? Reason);
