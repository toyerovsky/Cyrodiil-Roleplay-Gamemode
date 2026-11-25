using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Traders;

// stocks.json: the shops (a shop, its categories and their wares by editor id), the prices set by hand for wares the
// game values at nothing and for the custom ones, in whole gold, without the markup, and the wares that are custom items.
internal sealed record StockFile(
    [property: JsonPropertyName("prices")] Dictionary<string, int> Prices,
    [property: JsonPropertyName("custom")] Dictionary<string, CustomWareEntry> Custom,
    [property: JsonPropertyName("shops")] Dictionary<string, Dictionary<string, List<string>>> Shops);
