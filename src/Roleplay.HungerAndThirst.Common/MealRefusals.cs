namespace Roleplay.HungerAndThirst.Common;

/// <summary>The mod's own words for each <see cref="MealRefusal"/>. A mod with its own texts can translate the enum
/// itself instead.</summary>
public static class MealRefusals
{
    public static string Describe(MealRefusal refusal) => refusal switch
    {
        MealRefusal.NotInWorld => "Not in the world yet.",
        MealRefusal.FeederDead => "The dead don't eat.",
        MealRefusal.EaterDead => "They are past eating.",
        MealRefusal.TooSoon => "Still chewing.",
        MealRefusal.NotFood => "That can't be eaten or drunk.",
        MealRefusal.NotCarried => "You have none of that left.",
        MealRefusal.NoWater => "There is no water here to drink.",
        MealRefusal.WaterForOthers => "Only you can drink the water you are in.",
        MealRefusal.TooFar => "They are too far away.",
        MealRefusal.Other => "That didn't work.",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "Not a meal refusal."),
    };
}
