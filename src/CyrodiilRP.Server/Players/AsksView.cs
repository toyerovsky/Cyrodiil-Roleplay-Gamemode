using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Players;

// A player's asks, the oldest first. Answerable: the one Y and N answer, the oldest asked of the reader, 0 for none.
internal sealed record AsksView(
    [property: JsonPropertyName("asks")] IReadOnlyList<AskView> Asks,
    [property: JsonPropertyName("answerable")] int Answerable);
