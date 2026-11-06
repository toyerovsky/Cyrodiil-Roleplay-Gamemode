namespace CyrodiilRP.Server.Lore;

// One 5d20 roll of a trait.
internal sealed record LoreRollJson(IReadOnlyList<int> Dice, int Total);
