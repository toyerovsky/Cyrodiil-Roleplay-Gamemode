using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// A field as the editor builds its tabs and inputs. Part and Type in lower camel case (LorePart, LoreFieldType),
// Vis: "public" or "private". Kinds, RaceScoped and Historical serve only the page's stand-in server in a browser.
internal sealed record LoreFieldJson(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("part")] string Part,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("max")] int Max,
    [property: JsonPropertyName("vis")] string Vis,
    [property: JsonPropertyName("locked")] bool Locked,
    [property: JsonPropertyName("ownerOnly")] bool OwnerOnly,
    [property: JsonPropertyName("hint")] string Hint,
    [property: JsonPropertyName("choices")] IReadOnlyList<LoreChoiceJson>? Choices,
    [property: JsonPropertyName("needs")] string? Needs,
    [property: JsonPropertyName("group")] string? Group,
    [property: JsonPropertyName("kinds")] IReadOnlyList<string>? Kinds,
    [property: JsonPropertyName("raceScoped")] bool RaceScoped,
    [property: JsonPropertyName("historical")] bool Historical);
