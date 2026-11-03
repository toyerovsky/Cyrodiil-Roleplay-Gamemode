using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Traders;

// A trader's window, sent as it opens and again as the player's gold changes. The page names the wares itself.
internal sealed record TraderView(
    [property: JsonPropertyName("shop")] string Shop,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("most")] int Most,
    [property: JsonPropertyName("wares")] IReadOnlyList<TraderWareView> Wares);
