using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MenuView))]
internal sealed partial class MenuJson : JsonSerializerContext;
