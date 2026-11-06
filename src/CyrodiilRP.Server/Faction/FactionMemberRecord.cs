namespace CyrodiilRP.Server.Faction;

// Name: the character's full name, as the faction's roll lists it. LastSeenAt: when the character last left the game.
internal sealed record FactionMemberRecord(long CharacterId, string Name, int RankNumber, DateTimeOffset JoinedAt, DateTimeOffset LastSeenAt);
