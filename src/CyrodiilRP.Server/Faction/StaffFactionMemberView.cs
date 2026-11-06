namespace CyrodiilRP.Server.Faction;

// Online: the member's player number while in the game. Leader: holds the leader's rank.
internal sealed record StaffFactionMemberView(long Id, string Name, int Rank, bool Leader, int? Online, long LastSeenAt);
