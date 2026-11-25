using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Building;

// Where the architect's drawer lists a piece. Read by name from the config, and a page reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<PieceCategory>))]
internal enum PieceCategory
{
    Houses,

    Floors,

    Farm,

    Fences,

    Furniture,

    Decoration,
}
