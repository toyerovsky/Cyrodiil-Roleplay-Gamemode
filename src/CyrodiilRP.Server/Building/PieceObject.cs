using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Building;

// What a piece stands as: a mesh every player's game draws, or the game's own furniture (to sit on, sleep in) or
// container (to open), made by the server. Read by name from the config.
[JsonConverter(typeof(JsonStringEnumConverter<PieceObject>))]
internal enum PieceObject
{
    Mesh,

    Furniture,

    Container,
}
