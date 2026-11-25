using CyrodiilRP.Server.Fishing;
using Roleplay.HungerAndThirst.Server;

namespace CyrodiilRP.Server.Sustenance;

// Every fish and trophy of the catch as raw food for Hunger and Thirst: satiety by the item's weight, a little under what
// the table's cooked meat gives for its weight, more for a trophy, which is the bigger fish, and a little water.
internal static class FishFoods
{
    private const double BaseSatiety = 40;
    private const double SatietyPerWeight = 70;
    private const double TrophyTimes = 1.5;
    private const int Hydration = 10;

    public static readonly IReadOnlyList<CustomFood> All = [.. FishCatalog.Items.Select(ToFood)];

    private static CustomFood ToFood(FishItem fish) => new(fish.Key, fish.Name, FindSatiety(fish), Hydration);

    private static int FindSatiety(FishItem fish)
    {
        var satiety = BaseSatiety + SatietyPerWeight * fish.Fish.Weight;
        return (int)Math.Round(fish.Trophy ? satiety * TrophyTimes : satiety, MidpointRounding.AwayFromZero);
    }
}
