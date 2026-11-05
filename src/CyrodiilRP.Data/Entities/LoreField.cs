namespace CyrodiilRP.Data;

// One field of a character's lore card. A cleared field keeps its row for the visibility the owner chose.
public sealed class LoreField
{
    public long CharacterId { get; set; }

    // Stored, so never renamed.
    public required string FieldId { get; set; }

    // A lore catalog entry, for a field picked from the lore list.
    public string? CatalogId { get; set; }

    public string? Text { get; set; }

    // A trait's value.
    public int? Number { get; set; }

    // A trait that no longer changes.
    public bool Fixed { get; set; }

    // A trait's rolls, oldest first: "5d20:3,17,9,11,6=46;5d20:...".
    public string? Rolls { get; set; }

    public LoreVisibility Visibility { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
