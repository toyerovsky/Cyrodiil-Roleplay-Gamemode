using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Crafting;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ClassificationFile))]
internal sealed partial class ClassificationJson : JsonSerializerContext;
