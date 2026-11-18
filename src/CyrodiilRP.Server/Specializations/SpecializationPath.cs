using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Specializations;

// The three paths, three specializations each. A mentor teaches the specializations of one path.
[JsonConverter(typeof(JsonStringEnumConverter<SpecializationPath>))]
internal enum SpecializationPath
{
    Warrior,

    Mage,

    Support,
}
