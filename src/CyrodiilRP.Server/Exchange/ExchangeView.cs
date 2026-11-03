using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Exchange;

// A trade as one side sees it, sent at every change. ConfirmInMs: how long until a confirmation is taken.
internal sealed record ExchangeView(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("other")] string Other,
    [property: JsonPropertyName("carried")] int Carried,
    [property: JsonPropertyName("max")] int Max,
    [property: JsonPropertyName("confirmInMs")] long ConfirmInMs,
    [property: JsonPropertyName("mine")] ExchangeOfferView Mine,
    [property: JsonPropertyName("theirs")] ExchangeOfferView Theirs);
