using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Faction;

// A faction as one of its members reads it. Rank: the reader's own, by number. Grantable: the permissions the reader may
// give a rank or take from it. Every "can" is the server's own check, made again when the command runs.
internal sealed record FactionCardView(
    long Id,
    string Name,
    long FoundedAt,
    int Rank,
    bool CanInvite,
    bool CanAddRank,
    bool CanLeave,
    IReadOnlyList<FactionPermission> Grantable,
    IReadOnlyList<FactionRankView> Ranks,
    IReadOnlyList<FactionMemberView> Members);
