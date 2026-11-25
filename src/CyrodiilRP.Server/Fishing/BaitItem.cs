using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Server.Fishing;

// A bait. CustomKey: the custom item the mode makes of Base, or null when the bait is the game's own item Base. Price at
// the fisher, 0 for one the fisher does not sell. Icon: the page's painted one.
internal sealed record BaitItem(BaitKind Kind, string Name, string? CustomKey, FormId Base, BaitUse Use, int Price, float Weight, int Value, string Icon);
