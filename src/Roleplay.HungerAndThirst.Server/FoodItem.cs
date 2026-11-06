using ReadyM.Relay.Common.Oblivion;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>One edible item: its editor id (the field name in the SDK's item catalogue), form id, name and what it gives.</summary>
public sealed record FoodItem(string Key, FormId FormId, string Name, int Satiety, int Hydration);
