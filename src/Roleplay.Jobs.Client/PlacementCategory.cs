namespace Roleplay.Jobs.Client;

/// <summary>A page of the placement mode: the jobs' people and deposits, or the ores.</summary>
public sealed record PlacementCategory(string Name, IReadOnlyList<PlacementItem> Items);
