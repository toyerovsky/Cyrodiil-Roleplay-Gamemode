using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Party;

[JsonSerializable(typeof(PartyView))]
internal sealed partial class PartyJson : JsonSerializerContext;
