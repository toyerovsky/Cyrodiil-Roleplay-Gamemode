using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// op load: the editor. Name: the character's own, which the header shows. Header: how a stranger sees the character. Mode: "canon", "canonPlus" or "free".
internal sealed record LoreLoadAnswer(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("readOnly")] bool ReadOnly,
    [property: JsonPropertyName("fields")] IReadOnlyList<LoreFieldJson> Fields,
    [property: JsonPropertyName("card")] IReadOnlyDictionary<string, LoreValueJson> Card,
    [property: JsonPropertyName("raceHint")] LoreRaceHintJson? RaceHint,
    [property: JsonPropertyName("maxCard")] int MaxCard,
    [property: JsonPropertyName("used")] int Used,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("header")] string Header,
    [property: JsonPropertyName("namePublic")] bool NamePublic);
