namespace CyrodiilRP.Server.Dice;

// The board as it opens and the menu's dice tab: the player's set, every set to pick from, and their last rolls, newest
// first.
internal sealed record DiceView(
    string Set,
    IReadOnlyList<string> Sets,
    IReadOnlyList<DiceRollView> Recent,
    int MaxDice);
