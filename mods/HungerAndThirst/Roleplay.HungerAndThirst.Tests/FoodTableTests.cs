using ReadyM.Relay.Common.Oblivion;
using Roleplay.HungerAndThirst.Common;
using Roleplay.HungerAndThirst.Server;
using Xunit;
using Ingredients = ReadyM.Relay.Common.Oblivion.Items.Items.Ingredients;
using Potions = ReadyM.Relay.Common.Oblivion.Items.Items.Potions;

namespace Roleplay.HungerAndThirst.Tests;

public class FoodTableTests
{
    private static Dictionary<uint, FoodItem> Build(SustenanceConfig config, List<string> problems)
        => FoodTable.Build(FoodCatalog.Defaults, config, ItemCatalogue.Find, problems);

    [Fact]
    public void Every_default_names_its_item_by_the_catalogue_field()
    {
        foreach (FoodItem food in FoodCatalog.Defaults)
        {
            Assert.Equal(food.FormId, ItemCatalogue.Find(food.Key));
            Assert.InRange(food.Satiety, 0, SustenanceScale.Max);
            Assert.InRange(food.Hydration, 0, SustenanceScale.Max);
            Assert.True(food.Satiety + food.Hydration > 0, food.Key);
        }
    }

    [Fact]
    public void Defaults_are_unique_and_include_food_and_drink()
    {
        Assert.Equal(FoodCatalog.Defaults.Count, FoodCatalog.Defaults.Select(f => f.FormId.Value).Distinct().Count());
        Assert.Contains(FoodCatalog.Defaults, f => f.FormId == Ingredients.Apple && f.Key == "Apple");
        Assert.Contains(FoodCatalog.Defaults, f => f.FormId == Potions.DrinkAle && f.Hydration > 0);
    }

    [Fact]
    public void The_catalogue_finds_items_by_editor_id_ignoring_case()
    {
        Assert.Equal(Ingredients.Breadloaf, ItemCatalogue.Find("breadloaf"));
        Assert.Null(ItemCatalogue.Find("NotAnItem"));
    }

    [Fact]
    public void Config_changes_add_override_and_remove()
    {
        List<string> problems = [];
        SustenanceConfig config = new()
        {
            Foods =
            [
                new FoodEntry { Item = "Apple", Satiety = 80, Hydration = 40 },
                new FoodEntry { Item = "Ambrosia", Satiety = 600 },
                new FoodEntry { Item = $"0x{Ingredients.Garlic.Value:X8}", Remove = true },
            ],
        };

        Dictionary<uint, FoodItem> table = Build(config, problems);

        Assert.Empty(problems);
        Assert.Equal(80, table[Ingredients.Apple.Value].Satiety);
        Assert.Equal("Apple", table[Ingredients.Apple.Value].Name); // default name kept
        Assert.Equal("Ambrosia", table[Ingredients.Ambrosia.Value].Name); // editor id as the name
        Assert.False(table.ContainsKey(Ingredients.Garlic.Value));
        Assert.Equal(FoodCatalog.Defaults.Count, table.Count); // one added, one removed
    }

    [Fact]
    public void Bad_lines_are_reported_and_skipped()
    {
        List<string> problems = [];
        SustenanceConfig config = new()
        {
            Foods =
            [
                new FoodEntry { Item = "Applee", Satiety = 50 },
                new FoodEntry { Item = "Pear", Satiety = 5000 },
                new FoodEntry { Item = "Pear" },
                new FoodEntry { Item = "0xZZ", Satiety = 50 },
            ],
        };

        Dictionary<uint, FoodItem> table = Build(config, problems);

        Assert.Equal(4, problems.Count);
        Assert.Equal(60, table[Ingredients.Pear.Value].Satiety); // untouched default
    }

    [Fact]
    public void Without_defaults_only_config_lines_count()
    {
        SustenanceConfig config = new()
        {
            UseDefaultFoods = false,
            Foods = [new FoodEntry { Item = "DrinkMead", Name = "Honey mead", Hydration = 250 }],
        };

        Dictionary<uint, FoodItem> table = Build(config, []);

        FoodItem mead = Assert.Single(table.Values);
        Assert.Equal(new FoodItem("DrinkMead", Potions.DrinkMead, "Honey mead", 0, 250), mead);
    }
}
