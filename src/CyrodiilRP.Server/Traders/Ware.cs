namespace CyrodiilRP.Server.Traders;

// A thing a trader sells, by the game's editor id, which also names its text and icon on the page. Price is one piece's.
// CustomKey: the key of the custom item a ware of the mode's own is, null for the game's. FormId is 0 for the mode's.
internal sealed record Ware(string Id, uint FormId, string? CustomKey, string Category, int Price, float Weight);
