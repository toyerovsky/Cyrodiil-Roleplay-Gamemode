namespace CyrodiilRP.Server.Lore;

// The dice of a trait roll just made. Reroll: it was the one reroll.
internal sealed record LoreRollAnswerJson(IReadOnlyList<int> Dice, int Total, bool Reroll);
