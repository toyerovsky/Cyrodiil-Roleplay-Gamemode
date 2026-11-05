using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// An entry as mesol-lore.json writes it. The sources and notes stay in the file.
internal sealed record LoreCatalogFileEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("race")] string? Race,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parent")] string? Parent,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("era")] string Era,
    [property: JsonPropertyName("modelFit")] string? ModelFit,
    [property: JsonPropertyName("remasteredOrigin")] bool RemasteredOrigin);
