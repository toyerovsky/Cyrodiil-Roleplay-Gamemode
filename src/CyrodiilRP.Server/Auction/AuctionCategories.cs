namespace CyrodiilRP.Server.Auction;

// The game's record types of each kind of goods.
internal static class AuctionCategories
{
    private static readonly string[] Weapons = ["WEAP", "AMMO"];
    private static readonly string[] Armour = ["ARMO"];
    private static readonly string[] Clothing = ["CLOT"];
    private static readonly string[] Potions = ["ALCH"];
    private static readonly string[] Ingredients = ["INGR"];
    private static readonly string[] Books = ["BOOK"];
    private static readonly string[] Other = ["MISC", "APPA", "SLGM", "SGST", "LIGH", "KEYM"];

    // Empty for All.
    public static IReadOnlyList<string> ListTypes(AuctionCategory category) => category switch
    {
        AuctionCategory.All => [],
        AuctionCategory.Weapons => Weapons,
        AuctionCategory.Armour => Armour,
        AuctionCategory.Clothing => Clothing,
        AuctionCategory.Potions => Potions,
        AuctionCategory.Ingredients => Ingredients,
        AuctionCategory.Books => Books,
        AuctionCategory.Other => Other,
    };

    public static AuctionCategory Find(string type)
    {
        foreach (var category in Enum.GetValues<AuctionCategory>())
        {
            if (category != AuctionCategory.All && ListTypes(category).Contains(type))
                return category;
        }

        throw new InvalidOperationException($"The record type {type} is in no kind of goods.");
    }
}
