using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Building;

// Which plots a piece may stand on. Read by name from the config, and a page reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<PieceUse>))]
internal enum PieceUse
{
    Residential,

    Industrial,

    Any,
}
