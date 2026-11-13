using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Jobs;

[JsonSerializable(typeof(ForemanView))]
internal sealed partial class ForemanJson : JsonSerializerContext;
