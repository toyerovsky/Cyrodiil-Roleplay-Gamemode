using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Party;

// Self: the reader's own row, which the page marks as "you". Health: in percent, in steps of PartyRules.HealthStep,
// null while the member is not in the world. Away: out of the reader's sight (PartyRules.Near) or in another place.
internal sealed record PartyMemberView(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("leader")] bool Leader,
    [property: JsonPropertyName("self")] bool Self,
    [property: JsonPropertyName("health")] int? Health,
    [property: JsonPropertyName("away")] bool Away);
