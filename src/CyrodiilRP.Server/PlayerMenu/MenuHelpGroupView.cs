using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

// Category: the heading's id, which the page names.
internal sealed record MenuHelpGroupView(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("commands")] IReadOnlyList<MenuCommandView> Commands);
