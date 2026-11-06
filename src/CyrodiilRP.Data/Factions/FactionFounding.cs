namespace CyrodiilRP.Data.Factions;

// A new faction: its name, its founder and the names of the two ranks it starts with.
public sealed record FactionFounding(string Name, long FounderCharacterId, string LeaderRankName, string EntryRankName, DateTimeOffset At);
