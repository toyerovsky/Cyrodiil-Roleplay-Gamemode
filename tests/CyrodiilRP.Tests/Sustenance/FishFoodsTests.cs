using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Server.Sustenance;
using Roleplay.HungerAndThirst.Common;
using Roleplay.HungerAndThirst.Server;
using Ingredients = ReadyM.Relay.Common.Oblivion.Items.Items.Ingredients;

namespace CyrodiilRP.Tests.Sustenance;

public sealed class FishFoodsTests
{
    [Fact]
    public void Every_fish_and_trophy_of_the_catch_is_food_once_under_its_item_key()
    {
        Assert.Equal(FishCatalog.Items.Select(fish => fish.Key).Order(), FishFoods.All.Select(food => food.Key).Order());
        Assert.All(FishFoods.All, food => Assert.Equal(FishCatalog.FindItem(food.Key)!.Name, food.Name));
    }

    [Fact]
    public void Hunger_and_thirst_takes_every_fish()
    {
        var registry = new CustomFoodRegistry();

        foreach (var food in FishFoods.All)
            registry.Register(food);

        Assert.Equal(FishFoods.All.Count, registry.All.Count);
        Assert.All(FishFoods.All, food => Assert.InRange(food.Satiety, 1, SustenanceScale.Max));
    }

    [Fact]
    public void A_heavier_fish_feeds_more_and_a_trophy_more_than_its_plain_fish()
    {
        var plain = FishCatalog.All.OrderBy(fish => fish.Weight).Select(fish => Find(fish.ItemKey).Satiety).ToList();

        Assert.Equal(plain.Order(), plain);
        Assert.All(FishCatalog.All, fish => Assert.True(Find(fish.TrophyKey).Satiety > Find(fish.ItemKey).Satiety, fish.ItemKey));
    }

    [Fact]
    public void A_raw_fish_feeds_less_than_the_tables_beef()
    {
        var beef = FoodCatalog.Defaults.Single(food => food.FormId == Ingredients.Beef);

        Assert.All(FishCatalog.All, fish => Assert.True(Find(fish.ItemKey).Satiety < beef.Satiety, fish.ItemKey));
    }

    private static CustomFood Find(string key) => FishFoods.All.Single(food => food.Key == key);
}
