namespace Roleplay.HungerAndThirst.Server;

/// <summary>A custom item another mod made (<c>ICustomItems</c>, by its key) that can be eaten or drunk: its name for the
/// log and what one gives, on the scale of <see cref="Common.SustenanceScale"/>.</summary>
public sealed record CustomFood(string Key, string Name, int Satiety, int Hydration);
