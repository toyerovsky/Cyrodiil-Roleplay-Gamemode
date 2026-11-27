using System.Text.Json.Serialization;

namespace CyrodiilRP.Common.Building;

// What architect mode is doing: choosing the plot to build on, marking a new plot, or building on one. The page and the
// client read it by name.
[JsonConverter(typeof(JsonStringEnumConverter<ArchitectTool>))]
public enum ArchitectTool
{
    Choose,

    Mark,

    Build,
}
