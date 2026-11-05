using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

// The server's own message to the page, request id 0: "edit" opens the editor.
internal sealed record LorePush(
    [property: JsonPropertyName("op")] string Op);
