namespace Roleplay.HungerAndThirst.Server;

/// <summary>A food a player carries, and how many.</summary>
public sealed record CarriedFood(FoodItem Food, int Count);
