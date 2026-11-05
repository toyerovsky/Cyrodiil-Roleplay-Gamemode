using System.Text.Json.Serialization;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Server.PlayerMenu;

// The menu's lore tab. Header: how a stranger sees the character. Preview: the card as the public reads it. Known: the
// players in play who know the character's name.
internal sealed record MenuLoreView(
    [property: JsonPropertyName("namePublic")] bool NamePublic,
    [property: JsonPropertyName("header")] string Header,
    [property: JsonPropertyName("preview")] LoreViewJson Preview,
    [property: JsonPropertyName("known")] IReadOnlyList<MenuKnownView> Known);
