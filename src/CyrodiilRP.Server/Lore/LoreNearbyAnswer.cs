namespace CyrodiilRP.Server.Lore;

// op nearby: the players within Range metres, nearest first.
internal sealed record LoreNearbyAnswer(bool Ok, IReadOnlyList<LoreNearbyPlayerJson> Players, double Range);
