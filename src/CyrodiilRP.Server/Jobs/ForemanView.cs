using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Jobs;

// A foreman's window, sent as it opens and again as the player's gold or goods change. Unpaid: crates delivered and not
// paid for, at Wage gold each, when the foreman pays wages.
internal sealed record ForemanView(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("most")] int Most,
    [property: JsonPropertyName("paysWages")] bool PaysWages,
    [property: JsonPropertyName("unpaid")] int Unpaid,
    [property: JsonPropertyName("wage")] int Wage,
    [property: JsonPropertyName("offers")] IReadOnlyList<ForemanOfferView> Offers);
