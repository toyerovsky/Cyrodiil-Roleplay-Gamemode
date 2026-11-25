using Roleplay.HungerAndThirst.Common;
using Roleplay.HungerAndThirst.Server;

namespace Roleplay.HungerAndThirst.Tests;

public class CustomFoodRegistryTests
{
    private static readonly CustomFood Perch = new("fishing.perch", "Silverside Perch", 75, 10);

    [Fact]
    public void A_registered_custom_food_is_found_by_its_key()
    {
        CustomFoodRegistry registry = new();

        registry.Register(Perch);

        Assert.True(registry.TryGet(Perch.Key, out CustomFood found));
        Assert.Equal(Perch, found);
        Assert.False(registry.TryGet("fishing.pike", out _));
        Assert.Equal([Perch], registry.All);
    }

    [Fact]
    public void A_key_is_food_once()
    {
        CustomFoodRegistry registry = new();
        registry.Register(Perch);

        Assert.Throws<InvalidOperationException>(() => registry.Register(Perch with { Satiety = 1 }));
        Assert.Equal(Perch.Satiety, Assert.Single(registry.All).Satiety);
    }

    [Theory]
    [InlineData("", "Perch", 50, 0)]
    [InlineData("fishing.perch", " ", 50, 0)]
    [InlineData("fishing.perch", "Perch", -1, 10)]
    [InlineData("fishing.perch", "Perch", 50, SustenanceScale.Max + 1)]
    [InlineData("fishing.perch", "Perch", 0, 0)]
    public void A_custom_food_without_a_key_a_name_or_values_on_the_scale_is_refused(string key, string name, int satiety, int hydration)
    {
        CustomFoodRegistry registry = new();

        Assert.Throws<ArgumentException>(() => registry.Register(new CustomFood(key, name, satiety, hydration)));
        Assert.Empty(registry.All);
    }
}
