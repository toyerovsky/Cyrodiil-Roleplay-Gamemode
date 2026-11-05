using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Dice;

// The board as it opens and the menu's dice tab: the player's set, every set to pick from, and their last rolls, newest
// first.
internal sealed record DiceView(
    [property: JsonPropertyName("set")] string Set,
    [property: JsonPropertyName("sets")] IReadOnlyList<string> Sets,
    [property: JsonPropertyName("recent")] IReadOnlyList<DiceRollView> Recent,
    [property: JsonPropertyName("maxDice")] int MaxDice);
