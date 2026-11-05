namespace CyrodiilRP.Server.Lore;

// One 5d20 roll of a trait: each die and their sum.
internal sealed record LoreTraitRoll(IReadOnlyList<int> Dice, int Total);
