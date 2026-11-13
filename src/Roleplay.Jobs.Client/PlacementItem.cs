namespace Roleplay.Jobs.Client;

/// <summary>One thing the placement mode places: Index is the foreman's role or the ore's kind, as the server lists them.</summary>
public sealed record PlacementItem(string Name, PlacementKind Kind, int Index);
