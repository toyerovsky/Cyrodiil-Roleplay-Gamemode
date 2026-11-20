using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Auction;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AuctionView))]
internal sealed partial class AuctionJson : JsonSerializerContext;
