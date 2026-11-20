namespace CyrodiilRP.Server.Crafting;

// Level: the trade level a character needs to make it. NeedsPattern: the level alone does not unlock it.
internal sealed record CraftProduct(Trade Trade, int Level, bool NeedsPattern);
