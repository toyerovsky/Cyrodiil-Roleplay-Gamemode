namespace CyrodiilRP.Server.Crafting;

// What an item of the game is to the crafting. Product: a trade makes it. Material: found or gathered, and used up by
// the trades. Relic: a quest's or a unique item, which only the staff hand out. Exempt: the crafting has no say over
// it (gold, keys, conjured gear, the game's test forms).
internal enum ItemClass
{
    Product,
    Material,
    Relic,
    Exempt,
}
