using System.Text.Json.Serialization;

namespace CyrodiilRP.Client.Building;

// The host turns reflection-based JSON off: the gizmo goes to the page through this context.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GizmoView))]
internal sealed partial class GizmoJson : JsonSerializerContext;
