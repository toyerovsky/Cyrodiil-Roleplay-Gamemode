using System.Text.Json.Serialization;

namespace CyrodiilRP.Client.Chat;

[JsonSerializable(typeof(List<PageLine>))]
internal sealed partial class PageJson : JsonSerializerContext;
