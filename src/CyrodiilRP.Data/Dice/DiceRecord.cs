namespace CyrodiilRP.Data.Dice;

// Set: null for the default. Recent: newest first.
public sealed record DiceRecord(string? Set, IReadOnlyList<DiceRoll> Recent);
