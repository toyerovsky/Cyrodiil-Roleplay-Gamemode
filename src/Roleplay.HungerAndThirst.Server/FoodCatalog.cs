using System.Runtime.CompilerServices;
using ReadyM.Relay.Common.Oblivion;
using Ingredients = ReadyM.Relay.Common.Oblivion.Items.Items.Ingredients;
using Potions = ReadyM.Relay.Common.Oblivion.Items.Items.Potions;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>
/// The default nutrition table. The SDK lists every item of the game by category (<c>Items.Ingredients</c>,
/// <c>Items.Potions</c>) but has no "food" flag, so this picks the everyday food and drink out of those lists.
/// Quest items, alchemy-only ingredients and potions are left out on purpose. Server owners change values, add items
/// and remove these in config.json (<see cref="SustenanceConfig.Foods"/>). Points are on the 0..1000 scale of
/// <see cref="Common.SustenanceScale"/>.
/// </summary>
public static class FoodCatalog
{
    public static readonly IReadOnlyList<FoodItem> Defaults =
    [
        // Fruit and vegetables: a little of both.
        Food(Ingredients.Apple, "Apple", 60, 30),
        Food(Ingredients.Pear, "Pear", 60, 30),
        Food(Ingredients.Orange, "Orange", 60, 40),
        Food(Ingredients.Grapes, "Grapes", 50, 40),
        Food(Ingredients.Strawberry, "Strawberry", 30, 20),
        Food(Ingredients.Blackberry, "Blackberry", 30, 20),
        Food(Ingredients.Watermelon, "Watermelon", 60, 150),
        Food(Ingredients.Tomato, "Tomato", 50, 40),
        Food(Ingredients.Lettuce, "Lettuce", 40, 20),
        Food(Ingredients.Carrot, "Carrot", 50, 10),
        Food(Ingredients.Corn, "Corn", 80, 0),
        Food(Ingredients.Potato, "Potato", 90, 0),
        Food(Ingredients.Pumpkin, "Pumpkin", 100, 20),
        Food(Ingredients.Radish, "Radish", 40, 10),
        Food(Ingredients.Leek, "Leek", 40, 10),
        Food(Ingredients.Onion, "Onion", 40, 10),
        Food(Ingredients.Garlic, "Garlic", 20, 0),
        Food(Ingredients.Rice, "Rice", 100, 0),

        // Bread, cheese and sweets.
        Food(Ingredients.Breadloaf, "Bread Loaf", 180, 0),
        Food(Ingredients.CheeseWedge, "Cheese Wedge", 100, 0),
        Food(Ingredients.CheeseWheel, "Cheese Wheel", 300, 0),
        Food(Ingredients.Sweetroll, "Sweetroll", 120, 0),
        Food(Ingredients.Sweetcake, "Sweetcake", 150, 0),

        // Meat: the most filling.
        Food(Ingredients.Beef, "Beef", 220, 0),
        Food(Ingredients.BoarMeat, "Boar Meat", 220, 0),
        Food(Ingredients.Venison, "Venison", 220, 0),
        Food(Ingredients.Mutton, "Mutton", 200, 0),
        Food(Ingredients.Ham, "Ham", 200, 0),
        Food(Ingredients.CrabMeat, "Crab Meat", 140, 10),
        Food(Ingredients.Ratmeat, "Rat Meat", 80, 0),

        // Drinks. Oblivion has no water item; water comes from swimming (SustenanceConfig.WaterHydration).
        Food(Potions.DrinkAle, "Ale", 30, 200),
        Food(Potions.DrinkBeer, "Beer", 30, 200),
        Food(Potions.DrinkMead, "Mead", 40, 180),
        Food(Potions.DrinkWine0Cheap, "Cheap Wine", 10, 160),
        Food(Potions.DrinkWine1SurilieGood, "Surilie Brothers Wine", 10, 180),
        Food(Potions.DrinkWine2TamikaGood, "Tamika's Wine", 10, 180),
        Food(Potions.DrinkWine3SurilieBetter, "Surilie Brothers Wine", 10, 180),
        Food(Potions.DrinkWine4TamikaBetter, "Tamika's Wine", 10, 180),
        Food(Potions.DrinkWine5SurilieBest, "Surilie Brothers Wine", 10, 180),
        Food(Potions.DrinkWine6TamikaBest, "Tamika's Wine", 10, 180),
        Food(Potions.PotionCyrodiilicBrandy, "Cyrodiilic Brandy", 10, 100),
    ];

    // The key is the field's name, taken from the argument as written ("Ingredients.Apple" -> "Apple"), so it
    // always matches the SDK catalogue that config.json names items by.
    private static FoodItem Food(FormId id, string name, int satiety, int hydration,
        [CallerArgumentExpression(nameof(id))] string expression = "")
    {
        string key = expression[(expression.LastIndexOf('.') + 1)..];
        return new FoodItem(key, id, name, satiety, hydration);
    }
}
