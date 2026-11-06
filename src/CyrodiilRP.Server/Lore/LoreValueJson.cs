namespace CyrodiilRP.Server.Lore;

// The owner's own value of a field, an empty one too for its visibility. Shown: how it reads, empty for an empty field and
// null for a field of the player's own words, which reads as its text. Summary: the picked entry's, null without one.
internal sealed record LoreValueJson(
    string? CatalogId,
    string? Text,
    string? Shown,
    string? Summary,
    int? Number,
    bool Fixed,
    IReadOnlyList<LoreRollJson>? Rolls,
    string Vis,
    bool Empty);
