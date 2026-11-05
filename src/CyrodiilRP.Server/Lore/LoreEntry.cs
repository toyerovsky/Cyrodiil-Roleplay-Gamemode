namespace CyrodiilRP.Server.Lore;

// One entry of the lore list. Race: the race's lore name for an entry of one race ("Khajiit"), "Mixed" for mixed
// ancestry, null for one shared by all. RemasteredOrigin: it comes from the Remaster, listed first.
internal sealed record LoreEntry(
    string Id,
    string? Race,
    LoreKind Kind,
    string Name,
    string? Parent,
    string Summary,
    LoreEra Era,
    LoreModelFit ModelFit,
    bool RemasteredOrigin)
{
    public const string MixedRace = "Mixed";
}
