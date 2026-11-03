using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

[JsonSerializable(typeof(MenuView))]
internal sealed partial class MenuJson : JsonSerializerContext;
