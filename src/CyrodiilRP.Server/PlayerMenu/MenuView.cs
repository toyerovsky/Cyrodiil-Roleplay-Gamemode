using System.Text.Json.Serialization;
using CyrodiilRP.Server.Dice;
using CyrodiilRP.Server.Faction;

namespace CyrodiilRP.Server.PlayerMenu;

// The player's menu as it opens, on the tab named (MenuTabs). Languages: the locales the server speaks, the picker shows
// only when there is more than one.
internal sealed record MenuView(
    [property: JsonPropertyName("tab")] string Tab,
    [property: JsonPropertyName("character")] MenuCharacterView Character,
    [property: JsonPropertyName("account")] MenuAccountView Account,
    [property: JsonPropertyName("help")] IReadOnlyList<MenuHelpGroupView> Help,
    [property: JsonPropertyName("dice")] DiceView Dice,
    [property: JsonPropertyName("lore")] MenuLoreView Lore,
    FactionMenuView Faction,
    [property: JsonPropertyName("sanctuary")] MenuSanctuaryView Sanctuary,
    [property: JsonPropertyName("languages")] IReadOnlyList<string> Languages,
    [property: JsonPropertyName("language")] string Language);
