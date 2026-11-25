using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Traders;

// A ware of stocks.json that is a custom item a mod makes: Item is its key. The research's export_wares.py reads its name
// and icon (a game item's editor id) for the page as well.
internal sealed record CustomWareEntry(
    [property: JsonPropertyName("item")] string Item,
    [property: JsonPropertyName("weight")] float Weight);
