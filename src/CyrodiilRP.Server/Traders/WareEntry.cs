using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Traders;

// A line of wares.json, which tools/ui/export_wares.py of the research writes from the game's own files.
internal sealed record WareEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("form")] string Form,
    [property: JsonPropertyName("value")] int Value,
    [property: JsonPropertyName("weight")] float Weight);
