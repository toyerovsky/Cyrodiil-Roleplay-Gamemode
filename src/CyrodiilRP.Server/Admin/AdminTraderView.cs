using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// A trader's spot, in centimetres of its area: /atp goes there by the coordinates.
internal sealed record AdminTraderView(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("shop")] string Shop,
    [property: JsonPropertyName("area")] string Area,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("z")] int Z);
