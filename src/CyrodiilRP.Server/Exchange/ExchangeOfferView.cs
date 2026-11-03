using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Exchange;

internal sealed record ExchangeOfferView(
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("confirmed")] bool Confirmed);
