using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

// In the player's language: "/roll [sides]" and what it does.
internal sealed record MenuCommandView(
    [property: JsonPropertyName("syntax")] string Syntax,
    [property: JsonPropertyName("description")] string Description);
