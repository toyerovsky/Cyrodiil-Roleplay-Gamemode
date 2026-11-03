using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Party;

// The party as the panel lists it: the leader first, then the order they joined. Empty: the player is in none. Max: how
// many a party holds.
internal sealed record PartyView(
    [property: JsonPropertyName("members")] IReadOnlyList<PartyMemberView> Members,
    [property: JsonPropertyName("max")] int Max);
