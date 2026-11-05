namespace CyrodiilRP.Data;

// Inventory, skills, looks and position live in the game's own saves.
public sealed class Character
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public Account? Account { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    // Lower case, so the unique index ignores letter case.
    public required string NameKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastPlayedAt { get; set; }

    public long PlayedSeconds { get; set; }

    // The dice set the player picked, null for the default.
    public string? DiceSet { get; set; }

    public static string MakeNameKey(string firstName, string lastName) => $"{firstName} {lastName}".ToLowerInvariant();
}
