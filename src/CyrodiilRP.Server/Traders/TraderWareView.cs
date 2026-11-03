using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Traders;

// Id: the game's editor id, which names the ware's text and icon on the page. Price is one piece's, in gold.
internal sealed record TraderWareView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("price")] int Price,
    [property: JsonPropertyName("weight")] float Weight);
