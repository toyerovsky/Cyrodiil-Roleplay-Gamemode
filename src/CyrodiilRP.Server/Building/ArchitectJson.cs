using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Building;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ArchitectView))]
[JsonSerializable(typeof(ArchitectPieceView))]
[JsonSerializable(typeof(PlotForm))]
internal sealed partial class ArchitectJson : JsonSerializerContext;
