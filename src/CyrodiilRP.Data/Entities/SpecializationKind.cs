using System.Text.Json.Serialization;

namespace CyrodiilRP.Data;

// The nine specializations, three in each path: the warrior's, the mage's and the support's. Stored by name, and a page
// reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<SpecializationKind>))]
public enum SpecializationKind
{
    Knight,

    Barbarian,

    Ranger,

    Wizard,

    Alchemist,

    Illusionist,

    Healer,

    Smith,

    Thief,
}
