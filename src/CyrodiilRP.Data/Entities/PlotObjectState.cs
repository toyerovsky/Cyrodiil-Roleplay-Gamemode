using System.Text.Json.Serialization;

namespace CyrodiilRP.Data;

// A piece placed on a plot: a blueprint shows where it will stand until its materials are in, a built one stands. Stored
// by name, and a page reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<PlotObjectState>))]
public enum PlotObjectState
{
    Blueprint,

    Built,
}
