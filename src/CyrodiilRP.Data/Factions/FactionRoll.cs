namespace CyrodiilRP.Data.Factions;

// Every faction as stored, with its ranks and its members.
public sealed record FactionRoll(IReadOnlyList<Faction> Factions, IReadOnlyList<FactionRank> Ranks, IReadOnlyList<FactionMemberRow> Members);
