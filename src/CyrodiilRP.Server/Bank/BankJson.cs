using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Bank;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BankView))]
internal sealed partial class BankJson : JsonSerializerContext;
