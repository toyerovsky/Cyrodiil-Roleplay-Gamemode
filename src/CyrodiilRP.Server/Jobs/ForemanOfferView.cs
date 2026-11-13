using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Jobs;

// Kind: Buy (the player buys it) or Sell (the player sells it). Icon: the game's editor id whose icon stands for it on
// the page, empty for none. Carried: how many the player has to sell.
internal sealed record ForemanOfferView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("icon")] string Icon,
    [property: JsonPropertyName("price")] int Price,
    [property: JsonPropertyName("carried")] int Carried)
{
    public const string Buy = "buy";
    public const string Sell = "sell";
}
