namespace CyrodiilRP.Data;

// Append only: what the staff read when a roll is in doubt.
public sealed class DiceRoll
{
    public long Id { get; set; }

    public DateTimeOffset At { get; set; }

    public long CharacterId { get; set; }

    // The name it was rolled under, as it was then.
    public required string CharacterName { get; set; }

    // As the chat line said it: "1d20 + 2d6".
    public required string Asked { get; set; }

    // Each die's sides and value in the order rolled: "20:14,6:3,6:5".
    public required string Dice { get; set; }

    public int Total { get; set; }

    // What a roll made for something else was for, otherwise null.
    public string? Reason { get; set; }

    // The game's id of the area it was rolled in, null when the server did not know it.
    public string? Area { get; set; }
}
