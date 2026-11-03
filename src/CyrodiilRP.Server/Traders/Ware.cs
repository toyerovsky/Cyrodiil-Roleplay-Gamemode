namespace CyrodiilRP.Server.Traders;

// A thing a trader sells, by the game's editor id, which also names its text and icon on the page. Price is one piece's.
internal sealed record Ware(string Id, uint FormId, string Category, int Price, float Weight);
