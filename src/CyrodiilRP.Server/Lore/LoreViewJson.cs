namespace CyrodiilRP.Server.Lore;

// A card cut to what its reader may see: the header line and the fields in the card's order. Level: "public", "private"
// or "staff".
internal sealed record LoreViewJson(string Header, string Level, IReadOnlyList<LoreViewFieldJson> Fields);
