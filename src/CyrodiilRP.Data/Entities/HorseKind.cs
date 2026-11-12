using System.Text.Json.Serialization;

namespace CyrodiilRP.Data;

// The game's five horses a stablemaster sells, by their coat, the cheapest first. Stored by name, and a page reads it by
// name.
[JsonConverter(typeof(JsonStringEnumConverter<HorseKind>))]
public enum HorseKind
{
    Paint,

    Bay,

    Chestnut,

    White,

    Black,
}
