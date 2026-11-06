namespace CyrodiilRP.Server.Faction;

// A faction in full, for the staff.
internal sealed record StaffFactionCardView(
    long Id,
    string Name,
    long FoundedAt,
    IReadOnlyList<StaffFactionRankView> Ranks,
    IReadOnlyList<StaffFactionMemberView> Members);
