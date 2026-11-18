using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Specializations;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MentorView))]
internal sealed partial class MentorJson : JsonSerializerContext;
