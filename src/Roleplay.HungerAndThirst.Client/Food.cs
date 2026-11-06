namespace Roleplay.HungerAndThirst.Client;

/// <summary>Something the server says can be eaten or drunk, and what it gives.</summary>
public sealed record Food(uint FormId, string Name, int Satiety, int Hydration);
