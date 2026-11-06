using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Faction;

// A rank, from the top down. Members: how many hold it. CanGive: the reader may give it to a member below them.
internal sealed record FactionRankView(
    int Number,
    string Name,
    FactionRankKind Kind,
    IReadOnlyList<FactionPermission> Permissions,
    int Members,
    bool CanRename,
    bool CanEdit,
    bool CanDelete,
    bool CanMoveUp,
    bool CanMoveDown,
    bool CanGive);
