using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Traders;

// stocks.json: the shops (a shop, its categories and their wares by editor id) and the prices set by hand for wares the
// game values at nothing, in whole gold, without the markup.
internal sealed record StockFile(
    [property: JsonPropertyName("prices")] Dictionary<string, int> Prices,
    [property: JsonPropertyName("shops")] Dictionary<string, Dictionary<string, List<string>>> Shops);
