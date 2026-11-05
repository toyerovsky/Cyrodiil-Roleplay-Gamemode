namespace CyrodiilRP.Server.Dice;

// What the board asked for, and the id its answer carries back.
internal sealed record DiceThrow(int RequestId, DicePick Pick);
