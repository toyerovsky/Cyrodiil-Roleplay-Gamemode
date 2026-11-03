using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

// A player online. Area and Health are null while the character is not in the world.
internal sealed record AdminPlayerView(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("roles")] IReadOnlyList<string> Roles,
    [property: JsonPropertyName("onDuty")] bool OnDuty,
    [property: JsonPropertyName("character")] bool HasCharacter,
    [property: JsonPropertyName("muted")] bool Muted,
    [property: JsonPropertyName("frozen")] bool Frozen,
    [property: JsonPropertyName("area")] string? Area,
    [property: JsonPropertyName("health")] int? Health,
    [property: JsonPropertyName("healthMax")] int? HealthMax);
