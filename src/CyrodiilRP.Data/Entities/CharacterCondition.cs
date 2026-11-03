namespace CyrodiilRP.Data;

public sealed class CharacterCondition
{
    public long CharacterId { get; set; }

    // Stored, so never renamed.
    public required string Key { get; set; }

    public DateTimeOffset AppliedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string? ByRef { get; set; }

    public string? Reason { get; set; }
}
