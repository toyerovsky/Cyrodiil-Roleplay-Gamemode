namespace CyrodiilRP.Server.Lore;

// How strict the fields picked from the lore list are.
internal enum LoreCatalogMode
{
    // Picks only.
    Canon,

    // The player's own words too, beside a pick or instead of one, marked as custom.
    CanonPlus,

    // The player's own words anywhere, nothing marked.
    Free,
}
