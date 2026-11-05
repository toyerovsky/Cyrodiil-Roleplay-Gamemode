using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Lore;

// One field's value. CatalogId: a pick from the lore list. Text: the player's words, alone or beside a pick. Number and
// Rolls: a trait's, its rolls oldest first, none when it was typed. Fixed: a trait that no longer changes.
internal sealed record LoreValue(
    string? CatalogId,
    string? Text,
    int? Number,
    bool Fixed,
    IReadOnlyList<LoreTraitRoll> Rolls,
    LoreVisibility Visibility)
{
    public bool IsEmpty => string.IsNullOrEmpty(CatalogId) && string.IsNullOrEmpty(Text) && Number is null;

    // Nothing in it, only who would read it.
    public static LoreValue Empty(LoreVisibility visibility) => new(null, null, null, false, [], visibility);
}
