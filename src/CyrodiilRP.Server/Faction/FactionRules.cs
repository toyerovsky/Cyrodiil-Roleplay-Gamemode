using System.Text.RegularExpressions;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Faction;

// Who may do what in a faction. A rank's place decides: nobody acts on a member of their own rank or above, nor on a
// rank that is not below their own, and nobody hands out a permission they do not hold.
internal static partial class FactionRules
{
    public const int MinNameLength = 3;

    public const int MaxNameLength = 32;

    public const int MinRankNameLength = 2;

    public const int MaxRankNameLength = 24;

    // The leader's rank and the entry rank among them.
    public const int MaxRanks = 10;

    public const int MaxMembers = 100;

    public const int RankUp = -1;

    public const int RankDown = 1;

    // An invitation is good for this long, on the card at the top of the screen only for the first AskPacing.Timeout.
    public static readonly TimeSpan InviteLasts = TimeSpan.FromMinutes(5);

    // One space between words, none around them.
    public static string CleanName(string name) => string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static Phrase? CheckName(string name)
    {
        if (name.Length is < MinNameLength or > MaxNameLength)
            return FactionText.ErrNameLength(MinNameLength, MaxNameLength);

        return Shape.IsMatch(name) ? null : FactionText.ErrNameShape;
    }

    public static Phrase? CheckRankName(FactionRecord faction, string name, FactionRankRecord? renamed)
    {
        if (name.Length is < MinRankNameLength or > MaxRankNameLength)
            return FactionText.ErrRankNameLength(MinRankNameLength, MaxRankNameLength);

        if (!Shape.IsMatch(name))
            return FactionText.ErrNameShape;

        return faction.Ranks.Any(rank => rank.Number != renamed?.Number && string.Equals(rank.Name, name, StringComparison.OrdinalIgnoreCase))
            ? FactionText.ErrRankNameTaken(name)
            : null;
    }

    public static bool Outranks(FactionRecord faction, FactionMemberRecord actor, FactionMemberRecord target) =>
        faction.GetPosition(actor) < faction.GetPosition(target);

    public static Phrase? CheckInvite(FactionRecord faction, FactionMemberRecord actor)
    {
        if (!faction.GetRank(actor).Allows(FactionPermission.Invite))
            return FactionText.ErrMayNotInvite;

        return faction.Members.Count >= MaxMembers ? FactionText.ErrFull(MaxMembers) : null;
    }

    public static Phrase? CheckKick(FactionRecord faction, FactionMemberRecord actor, FactionMemberRecord target)
    {
        if (!faction.GetRank(actor).Allows(FactionPermission.Kick))
            return FactionText.ErrMayNotKick;

        if (actor.CharacterId == target.CharacterId)
            return FactionText.ErrKickSelf;

        return Outranks(faction, actor, target) ? null : FactionText.ErrOutranked(target.Name);
    }

    // The last member may always leave: the faction goes with them.
    public static Phrase? CheckLeave(FactionRecord faction, FactionMemberRecord member) =>
        IsSoleLeader(faction, member) && faction.Members.Count > 1 ? FactionText.ErrSoleLeader : null;

    // A leader may make another member a leader, and step down once there is another.
    public static Phrase? CheckAssign(FactionRecord faction, FactionMemberRecord actor, FactionMemberRecord target, FactionRankRecord rank)
    {
        var own = faction.GetRank(actor);
        if (!own.Allows(FactionPermission.Ranks))
            return FactionText.ErrMayNotRanks;

        if (target.RankNumber == rank.Number)
            return FactionText.ErrSameRank(target.Name, rank.Name);

        if (actor.CharacterId == target.CharacterId)
        {
            if (!own.IsLeader)
                return FactionText.ErrOwnRank;

            return IsSoleLeader(faction, actor) ? FactionText.ErrSoleLeader : null;
        }

        if (!Outranks(faction, actor, target))
            return FactionText.ErrOutranked(target.Name);

        return MayGive(faction, actor, rank) ? null : FactionText.ErrRankNotBelow;
    }

    // A rank below the giver's own, and the leader's rank from a leader.
    public static bool MayGive(FactionRecord faction, FactionMemberRecord actor, FactionRankRecord rank)
    {
        var own = faction.GetRank(actor);
        return own.Allows(FactionPermission.Ranks) && (IsBelow(faction, rank, own) || (rank.IsLeader && own.IsLeader));
    }

    // Its permissions, its place and its removal.
    public static Phrase? CheckRankChange(FactionRecord faction, FactionMemberRecord actor, FactionRankRecord rank)
    {
        var own = faction.GetRank(actor);
        if (!own.Allows(FactionPermission.Ranks))
            return FactionText.ErrMayNotRanks;

        return IsBelow(faction, rank, own) ? null : FactionText.ErrRankNotBelow;
    }

    // A leader names every rank, the leader's own too.
    public static Phrase? CheckMayRename(FactionRecord faction, FactionMemberRecord actor, FactionRankRecord rank)
    {
        var own = faction.GetRank(actor);
        if (!own.Allows(FactionPermission.Ranks))
            return FactionText.ErrMayNotRanks;

        return own.IsLeader || IsBelow(faction, rank, own) ? null : FactionText.ErrRankNotBelow;
    }

    public static Phrase? CheckRankRename(FactionRecord faction, FactionMemberRecord actor, FactionRankRecord rank, string name) =>
        CheckMayRename(faction, actor, rank) ?? CheckRankName(faction, name, rank);

    public static Phrase? CheckGrant(FactionRecord faction, FactionMemberRecord actor, FactionRankRecord rank, FactionPermission permission)
    {
        if (CheckRankChange(faction, actor, rank) is { } refusal)
            return refusal;

        return faction.GetRank(actor).Allows(permission) ? null : FactionText.ErrGrantUnheld;
    }

    // A new rank stands right above the entry rank, so one holding the entry rank would make a rank above their own.
    public static Phrase? CheckMayAddRank(FactionRecord faction, FactionMemberRecord actor)
    {
        var own = faction.GetRank(actor);
        if (!own.Allows(FactionPermission.Ranks))
            return FactionText.ErrMayNotRanks;

        if (own.Kind == FactionRankKind.Entry)
            return FactionText.ErrRankNotBelow;

        return faction.Ranks.Count >= MaxRanks ? FactionText.ErrRanksFull(MaxRanks) : null;
    }

    public static Phrase? CheckRankAdd(FactionRecord faction, FactionMemberRecord actor, string name) =>
        CheckMayAddRank(faction, actor) ?? CheckRankName(faction, name, renamed: null);

    public static Phrase? CheckRankDelete(FactionRecord faction, FactionMemberRecord actor, FactionRankRecord rank) =>
        CheckRankChange(faction, actor, rank) ?? CheckMade(rank);

    // Between the leader's rank and the entry rank, and never up to the mover's own place.
    public static Phrase? CheckRankMove(FactionRecord faction, FactionMemberRecord actor, FactionRankRecord rank, int step)
    {
        if ((CheckRankChange(faction, actor, rank) ?? CheckMade(rank)) is { } refusal)
            return refusal;

        var place = faction.GetPosition(rank) + step;
        if (place < 1 || place > faction.Ranks.Count - 2)
            return FactionText.ErrRankEdge;

        return place > faction.GetPosition(actor) ? null : FactionText.ErrRankNotBelow;
    }

    private static bool IsBelow(FactionRecord faction, FactionRankRecord rank, FactionRankRecord than) =>
        faction.GetPosition(rank) > faction.GetPosition(than);

    private static bool IsSoleLeader(FactionRecord faction, FactionMemberRecord member) =>
        faction.GetRank(member).IsLeader && faction.CountHolders(faction.LeaderRank) == 1;

    // The two ranks every faction has stay where they are.
    private static Phrase? CheckMade(FactionRankRecord rank) => rank.Kind switch
    {
        FactionRankKind.Custom => null,
        FactionRankKind.Leader => FactionText.ErrRankFixed,
        FactionRankKind.Entry => FactionText.ErrRankFixed,
    };

    // Latin letters and digits, with single spaces, apostrophes and hyphens between them, as a character's name: a
    // Cyrillic "a" would pass for another faction's name.
    [GeneratedRegex(@"^[\p{L}-[^A-Za-zÀ-ɏ]](?:[0-9]|[\p{L}-[^A-Za-zÀ-ɏ]]|[ '-](?=[0-9]|[\p{L}-[^A-Za-zÀ-ɏ]]))*$")]
    private static partial Regex Shape { get; }
}
