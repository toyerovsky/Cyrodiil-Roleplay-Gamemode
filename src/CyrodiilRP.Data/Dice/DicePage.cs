namespace CyrodiilRP.Data.Dice;

// More: there are older rolls below the last one.
public sealed record DicePage(IReadOnlyList<DiceRoll> Rolls, bool More);
