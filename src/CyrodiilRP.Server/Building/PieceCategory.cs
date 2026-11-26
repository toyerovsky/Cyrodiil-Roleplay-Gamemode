using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Building;

// Where the architect's drawer lists a piece. Read by name from the config, and a page reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<PieceCategory>))]
internal enum PieceCategory
{
    Houses,

    // The parts of the game's houses, by kit.
    Kits,

    Floors,

    Farm,

    Fences,

    Doors,

    Furniture,

    Decoration,
}
