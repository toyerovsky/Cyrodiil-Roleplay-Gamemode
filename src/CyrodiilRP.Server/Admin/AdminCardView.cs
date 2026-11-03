using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// One player in full. Dates in Unix milliseconds. Inventory is null while the character is not in the world, Position
// is in centimetres.
internal sealed record AdminCardView(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("accountCreatedAt")] long AccountCreatedAt,
    [property: JsonPropertyName("previousVisitAt")] long? PreviousVisitAt,
    [property: JsonPropertyName("characterCreatedAt")] long? CharacterCreatedAt,
    [property: JsonPropertyName("playedSeconds")] long PlayedSeconds,
    [property: JsonPropertyName("area")] string? Area,
    [property: JsonPropertyName("position")] string? Position,
    [property: JsonPropertyName("sanctions")] IReadOnlyList<AdminSanctionView> Sanctions,
    [property: JsonPropertyName("history")] IReadOnlyList<AdminAuditEntryView> History,
    [property: JsonPropertyName("inventory")] IReadOnlyList<AdminItemView>? Inventory);
