using System.Text.Json.Serialization;

namespace CyrodiilRP.Data;

// What a rank lets its members do in their faction. Stored by name, and a page reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<FactionPermission>))]
public enum FactionPermission
{
    Invite,
    Kick,
    Ranks,
}
