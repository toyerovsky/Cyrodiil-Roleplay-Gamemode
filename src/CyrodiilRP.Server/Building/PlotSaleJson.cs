using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Building;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PlotSaleView))]
internal sealed partial class PlotSaleJson : JsonSerializerContext;
