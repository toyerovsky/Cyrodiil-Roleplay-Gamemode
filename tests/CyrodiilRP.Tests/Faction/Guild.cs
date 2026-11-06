using CyrodiilRP.Data;
using CyrodiilRP.Server.Faction;

namespace CyrodiilRP.Tests.Faction;

// The Fighters Guild as a test finds it: Vilena its Master, Burz a Warder who invites, removes and manages ranks, and
// Lum an Associate, the entry rank.
internal sealed class Guild
{
    public const long LeaderId = 1;
    public const long OfficerId = 2;
    public const long MemberId = 3;
    public const string Name = "Fighters Guild";

    public static readonly DateTimeOffset Founded = new(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);

    private long _lastId = MemberId;

    public Guild()
    {
        Faction = FactionRecord.Found(7, Name, "Master", "Associate", Enlist(LeaderId, "Vilena Donton", FactionRank.LeaderNumber), Founded);
        var warder = AddRank("Warder", FactionPermission.Invite, FactionPermission.Kick, FactionPermission.Ranks);
        Faction.PutMember(Enlist(OfficerId, "Burz gro-Khash", warder.Number));
        Faction.PutMember(Enlist(MemberId, "Lum gro-Baroth", FactionRank.EntryNumber));
    }

    public FactionRecord Faction { get; }

    public FactionMemberRecord Leader => Faction.FindMember(LeaderId)!;

    public FactionMemberRecord Officer => Faction.FindMember(OfficerId)!;

    public FactionMemberRecord Member => Faction.FindMember(MemberId)!;

    public FactionRankRecord Warder => Faction.GetRank(Officer);

    public FactionRankRecord AddRank(string name, params FactionPermission[] permissions)
    {
        var rank = new FactionRankRecord(Faction.NextRankNumber, name, FactionRankKind.Custom, permissions);
        Faction.AddRank(rank);
        return rank;
    }

    public FactionMemberRecord Add(string name, FactionRankRecord rank)
    {
        var member = Enlist(++_lastId, name, rank.Number);
        Faction.PutMember(member);
        return member;
    }

    private static FactionMemberRecord Enlist(long id, string name, int rank) => new(id, name, rank, Founded, Founded);
}
