using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// One player in full. Dates in Unix milliseconds. Inventory is null while the character is not in the world, Position
// is in centimetres. Specialization: its name in the staff member's language, null before the first choice;
// SpecializationId: the same by the name the staff commands take. Stats: every attribute and skill with its cap, null
// while the specialization is not read.
internal sealed record AdminCardView(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("accountCreatedAt")] long AccountCreatedAt,
    [property: JsonPropertyName("previousVisitAt")] long? PreviousVisitAt,
    [property: JsonPropertyName("characterCreatedAt")] long? CharacterCreatedAt,
    [property: JsonPropertyName("playedSeconds")] long PlayedSeconds,
    [property: JsonPropertyName("specialization")] string? Specialization,
    [property: JsonPropertyName("specializationId")] string? SpecializationId,
    [property: JsonPropertyName("stats")] IReadOnlyList<AdminStatView>? Stats,
    [property: JsonPropertyName("area")] string? Area,
    [property: JsonPropertyName("position")] string? Position,
    [property: JsonPropertyName("sanctions")] IReadOnlyList<AdminSanctionView> Sanctions,
    [property: JsonPropertyName("history")] IReadOnlyList<AdminAuditEntryView> History,
    [property: JsonPropertyName("inventory")] IReadOnlyList<AdminItemView>? Inventory);
