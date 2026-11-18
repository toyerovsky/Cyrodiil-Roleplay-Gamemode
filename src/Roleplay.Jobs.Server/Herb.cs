using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>A plant of the game, as herbs.json lists it: its base form, the ingredient it gives (both form ids in hex),
/// the name shown over it and the Alchemy it asks. EditorId only helps whoever edits the file.</summary>
public sealed record Herb(string Plant, string EditorId, string Ingredient, string Name, HerbTier Tier);
