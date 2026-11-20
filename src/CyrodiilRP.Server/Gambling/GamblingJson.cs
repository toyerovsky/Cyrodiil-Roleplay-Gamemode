using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Gambling;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(GamblingView))]
[JsonSerializable(typeof(GamblingJournal))]
[JsonSerializable(typeof(GamblingPageView))]
internal sealed partial class GamblingJson : JsonSerializerContext;
