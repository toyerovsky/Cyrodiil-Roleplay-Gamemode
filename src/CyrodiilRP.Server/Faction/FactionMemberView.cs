namespace CyrodiilRP.Server.Faction;

// A member on the faction's roll. Id: the character's, as "#id" in a command. Rank: by number. LastSeenAt in Unix
// milliseconds. CanRank: the reader may give them at least one other rank.
internal sealed record FactionMemberView(
    long Id,
    string Name,
    int Rank,
    bool Online,
    long LastSeenAt,
    bool Self,
    bool CanKick,
    bool CanRank);
