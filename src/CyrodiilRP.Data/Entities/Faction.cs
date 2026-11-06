namespace CyrodiilRP.Data;

// A lasting organisation of characters: a guild, a gang, a house or a company.
public sealed class Faction
{
    public long Id { get; set; }

    public required string Name { get; set; }

    // Lower case, so the unique index ignores letter case.
    public required string NameKey { get; set; }

    public DateTimeOffset FoundedAt { get; set; }

    public static string MakeNameKey(string name) => name.ToLowerInvariant();
}
