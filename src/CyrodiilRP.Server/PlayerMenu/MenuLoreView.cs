using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Server.PlayerMenu;

// The menu's lore tab. Header: how a stranger sees the character. Preview: the card as the public reads it. Known: the
// players in play who know the character's name.
internal sealed record MenuLoreView(
    bool NamePublic,
    string Header,
    LoreViewJson Preview,
    IReadOnlyList<MenuKnownView> Known);
