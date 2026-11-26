using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Building;

// What a piece stands as: a mesh every player's game draws, or the game's own furniture (to sit on, sleep in), container
// (to open) or door (to swing open and shut), made by the server. Read by name from the config.
[JsonConverter(typeof(JsonStringEnumConverter<PieceObject>))]
internal enum PieceObject
{
    Mesh,

    Furniture,

    Container,

    // A door the game animates open and shut where it stands, never one that leads elsewhere.
    Door,
}
