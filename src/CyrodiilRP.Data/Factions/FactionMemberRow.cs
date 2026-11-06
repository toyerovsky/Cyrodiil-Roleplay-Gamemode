namespace CyrodiilRP.Data.Factions;

// A member with what its character's row says of it.
public sealed record FactionMemberRow(
    long CharacterId,
    long FactionId,
    int RankNumber,
    DateTimeOffset JoinedAt,
    string FirstName,
    string LastName,
    DateTimeOffset LastPlayedAt);
