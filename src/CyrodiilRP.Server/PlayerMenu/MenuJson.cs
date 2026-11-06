using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MenuView))]
internal sealed partial class MenuJson : JsonSerializerContext;
