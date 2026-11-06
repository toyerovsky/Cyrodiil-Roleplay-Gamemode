using System.Text.Json.Serialization;

namespace CyrodiilRP.Data;

// Stored by name, and a page reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<FactionRankKind>))]
public enum FactionRankKind
{
    // The top rank, holding every permission. A faction always has it.
    Leader,

    // A rank the faction made for itself.
    Custom,

    // The bottom rank, which a new member gets. A faction always has it.
    Entry,
}
