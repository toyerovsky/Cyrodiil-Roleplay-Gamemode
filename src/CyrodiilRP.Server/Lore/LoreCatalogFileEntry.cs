namespace CyrodiilRP.Server.Lore;

// An entry as mesol-lore.json writes it. The sources and notes stay in the file.
internal sealed record LoreCatalogFileEntry(
    string Id,
    string? Race,
    string Kind,
    string Name,
    string? Parent,
    string Summary,
    string Era,
    string? ModelFit,
    bool RemasteredOrigin);
