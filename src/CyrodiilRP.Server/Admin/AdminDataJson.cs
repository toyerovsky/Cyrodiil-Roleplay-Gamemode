using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

[JsonSerializable(typeof(List<PlaceEntry>))]
[JsonSerializable(typeof(List<ItemEntry>))]
[JsonSerializable(typeof(List<WeatherEntry>))]
internal sealed partial class AdminDataJson : JsonSerializerContext;
