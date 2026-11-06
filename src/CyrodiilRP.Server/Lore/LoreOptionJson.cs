namespace CyrodiilRP.Server.Lore;

// An entry a picker offers. Group: the heading it goes under (the homeland's native lands first), null for none.
internal sealed record LoreOptionJson(
    string? Group,
    string Id,
    string Name,
    string Summary,
    string Kind,
    string? Race,
    bool Origin,
    bool Historical);
