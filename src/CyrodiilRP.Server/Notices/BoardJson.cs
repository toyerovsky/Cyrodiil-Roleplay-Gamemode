using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Notices;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BoardView))]
internal sealed partial class BoardJson : JsonSerializerContext
{
    // Letters as they are, not as escapes six bytes long, so a full board fits one message (NoticeBoardSettings.Check). The
    // page parses the JSON and never puts it into HTML as it is. Options built here do not take the context's naming policy.
    public static BoardJson Letters { get; } = new(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });
}
