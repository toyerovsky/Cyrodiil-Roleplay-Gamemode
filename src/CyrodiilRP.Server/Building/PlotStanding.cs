using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Building;

// A plot as the character at its sign sees it. A page reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<PlotStanding>))]
internal enum PlotStanding
{
    ForSale,

    Yours,

    // Another character's.
    Taken,
}
