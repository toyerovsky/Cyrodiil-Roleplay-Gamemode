using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// op preview: the owner's card as a stranger or an acquaintance reads it.
internal sealed record LorePreviewAnswer(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("view")] LoreViewJson View);
