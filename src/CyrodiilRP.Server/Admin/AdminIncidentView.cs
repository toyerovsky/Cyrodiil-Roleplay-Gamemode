using System.Text.Json.Serialization;
using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Admin;

// One incident of the Sanctuary. At in Unix milliseconds, Victim the real name of a player or "NPC <ref id>", Damage the
// health lost all told, Aggressors the players who lost the protection for it.
internal sealed record AdminIncidentView(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("at")] long At,
    [property: JsonPropertyName("area")] string Area,
    [property: JsonPropertyName("areaName")] string AreaName,
    [property: JsonPropertyName("victim")] string Victim,
    [property: JsonPropertyName("victimKind"), JsonConverter(typeof(JsonStringEnumConverter<SanctuaryVictim>))] SanctuaryVictim VictimKind,
    [property: JsonPropertyName("damage")] float Damage,
    [property: JsonPropertyName("died")] bool Died,
    [property: JsonPropertyName("verdict"), JsonConverter(typeof(JsonStringEnumConverter<SanctuaryVerdict>))] SanctuaryVerdict Verdict,
    [property: JsonPropertyName("aggressors")] IReadOnlyList<string> Aggressors);
