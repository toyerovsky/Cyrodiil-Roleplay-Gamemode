using System.Text.Json.Serialization;

namespace CyrodiilRP.Data;

// What a plot is for: houses, or farm land and work buildings (an inn, a barn, a coop). Stored by name, and a page reads
// it by name.
[JsonConverter(typeof(JsonStringEnumConverter<PlotKind>))]
public enum PlotKind
{
    Residential,

    Industrial,
}
