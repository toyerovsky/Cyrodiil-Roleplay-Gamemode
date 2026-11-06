namespace CyrodiilRP.Server.Faction;

// A member together with the faction they are in.
internal sealed record FactionMembership(FactionRecord Faction, FactionMemberRecord Member)
{
    public FactionRankRecord Rank => Faction.GetRank(Member);
}
