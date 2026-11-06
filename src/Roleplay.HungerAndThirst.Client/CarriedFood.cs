namespace Roleplay.HungerAndThirst.Client;

/// <summary>A food the player carries: how many, minus one on its way to the server.</summary>
public sealed record CarriedFood(Food Food, int Count);
