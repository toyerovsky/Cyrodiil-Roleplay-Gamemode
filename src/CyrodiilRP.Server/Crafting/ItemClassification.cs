namespace CyrodiilRP.Server.Crafting;

// Product is set for ItemClass.Product and for no other class.
internal sealed record ItemClassification(ItemClass Class, CraftProduct? Product);
