using System.Text.Json.Serialization;

namespace CyrodiilRP.Client.Fishing;

// The host turns reflection-based JSON off: the angler's game reads the fishing page's view through this context.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AnglerView))]
internal sealed partial class AnglerViewJson : JsonSerializerContext;
