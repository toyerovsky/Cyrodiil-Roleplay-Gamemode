namespace Roleplay.HungerAndThirst.Server;

/// <summary>A tier ready to use: below <paramref name="Below"/> points (0: at empty) the character has the effects.</summary>
public sealed record Tier(string Name, int Below, EffectRecipe[] Effects);
