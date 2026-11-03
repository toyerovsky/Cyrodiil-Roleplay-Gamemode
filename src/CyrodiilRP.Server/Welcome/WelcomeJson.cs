using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Welcome;

[JsonSerializable(typeof(WelcomeView))]
internal sealed partial class WelcomeJson : JsonSerializerContext;
