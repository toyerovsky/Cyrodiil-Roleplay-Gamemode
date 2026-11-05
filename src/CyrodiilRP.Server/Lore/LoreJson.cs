using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

[JsonSerializable(typeof(LoreCatalogFile))]
[JsonSerializable(typeof(LoreLoadAnswer))]
[JsonSerializable(typeof(LoreEditAnswer))]
[JsonSerializable(typeof(LoreOptionsAnswer))]
[JsonSerializable(typeof(LorePreviewAnswer))]
[JsonSerializable(typeof(LoreNearbyAnswer))]
[JsonSerializable(typeof(LoreCardAnswer))]
[JsonSerializable(typeof(LoreMessageAnswer))]
[JsonSerializable(typeof(LorePush))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class LoreJson : JsonSerializerContext
{
    // A string goes to a player with its length in two bytes, so 65,534 bytes of UTF-8 at most: an answer stays under this.
    // A card written full in Chinese comes to 62 KB.
    public const int MaxAnswerBytes = 64_000;

    // Letters as they are, not as escapes six bytes long, so a card written in Polish fits one message. The page parses
    // the JSON and never puts it into HTML as it is.
    public static LoreJson Page { get; } = new(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
