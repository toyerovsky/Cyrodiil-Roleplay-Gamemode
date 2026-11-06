using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Faction;

// Number: the rank's name in commands and in the database, kept for good. Its place among the ranks is its faction's.
internal sealed record FactionRankRecord(int Number, string Name, FactionRankKind Kind, IReadOnlyList<FactionPermission> Permissions)
{
    public bool IsLeader => Kind == FactionRankKind.Leader;

    public bool Allows(FactionPermission permission) => IsLeader || Permissions.Contains(permission);
}
