using CyrodiilRP.Data;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Faction;

// What /faction does with ranks: one given to a member, a permission given or taken, and the ranks a faction makes,
// names, orders and deletes. FactionCommands picks the verb and finds the sender's faction.
[Service]
internal sealed partial class FactionRankCommands(Factions factions)
{
    private const string On = "on";
    private const string Off = "off";
    private const string Up = "up";
    private const string Down = "down";

    public void Assign(CommandCall call, FactionMembership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeMember(faction, factions.FindCharacter, rest, out var named) is not { } target)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UseRank : FactionText.ErrNoMember(rest));
            return;
        }

        if (FactionTargets.TakeRank(faction, named, out var left) is not { } rank || left.Length > 0)
        {
            call.Refuse(named.Length == 0 ? FactionText.UseRank : FactionText.ErrNoRank(named));
            return;
        }

        if (FactionRules.CheckAssign(faction, own.Member, target, rank) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        factions.SetRank(faction, target, rank);
        call.Reply(FactionText.RankSet(target.Name, rank.Name));
        factions.Tell(faction, FactionText.MemberRank(target.Name, rank.Name), except: call.Sender);
    }

    // The permission and then on or off. Without either, it turns.
    public void Grant(CommandCall call, FactionMembership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeRank(faction, rest, out var asked) is not { } rank)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UsePerm : FactionText.ErrNoRank(rest));
            return;
        }

        var words = asked.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > 2 || !TryPermission(words[0], out var permission))
        {
            call.Refuse(FactionText.UsePerm);
            return;
        }

        var held = rank.Permissions.Contains(permission);
        var grant = !held;
        if (words.Length == 2 && !TrySwitch(words[1], out grant))
        {
            call.Refuse(FactionText.UsePerm);
            return;
        }

        if (FactionRules.CheckGrant(faction, own.Member, rank, permission) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        if (held != grant)
        {
            IReadOnlyList<FactionPermission> permissions = grant
                ? [.. rank.Permissions, permission]
                : rank.Permissions.Where(each => each != permission).ToList();
            factions.ReplaceRank(faction, rank with { Permissions = permissions });
        }

        var label = FactionViews.LabelPermission(permission);
        call.Reply(grant ? FactionText.PermGranted(rank.Name, label) : FactionText.PermTaken(rank.Name, label));
    }

    public void Add(CommandCall call, FactionMembership own, string rest)
    {
        var name = FactionRules.CleanName(rest);
        if (name.Length == 0)
        {
            call.Refuse(FactionText.UseRankAdd);
            return;
        }

        if (FactionRules.CheckRankAdd(own.Faction, own.Member, name) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        factions.AddRank(own.Faction, name);
        call.Reply(FactionText.RankAdded(name));
    }

    public void Delete(CommandCall call, FactionMembership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeRank(faction, rest, out var left) is not { } rank || left.Length > 0)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UseRankDelete : FactionText.ErrNoRank(rest));
            return;
        }

        if (FactionRules.CheckRankDelete(faction, own.Member, rank) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        factions.RemoveRank(faction, rank);
        call.Reply(FactionText.RankDeleted(rank.Name, faction.EntryRank.Name));
    }

    public void Rename(CommandCall call, FactionMembership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeRank(faction, rest, out var asked) is not { } rank)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UseRankName : FactionText.ErrNoRank(rest));
            return;
        }

        var name = FactionRules.CleanName(asked);
        if (name.Length == 0)
        {
            call.Refuse(FactionText.UseRankName);
            return;
        }

        if (FactionRules.CheckRankRename(faction, own.Member, rank, name) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        factions.ReplaceRank(faction, rank with { Name = name });
        call.Reply(FactionText.RankRenamed(rank.Name, name));
    }

    public void Move(CommandCall call, FactionMembership own, string rest)
    {
        var faction = own.Faction;
        if (FactionTargets.TakeRank(faction, rest, out var direction) is not { } rank)
        {
            call.Refuse(rest.Length == 0 ? FactionText.UseRankMove : FactionText.ErrNoRank(rest));
            return;
        }

        if (!TryMove(direction, out var move))
        {
            call.Refuse(FactionText.UseRankMove);
            return;
        }

        if (FactionRules.CheckRankMove(faction, own.Member, rank, move) is { } refusal)
        {
            call.Refuse(refusal);
            return;
        }

        factions.MoveRank(faction, rank, move);
        call.Reply(move switch
        {
            FactionRankMove.Up => FactionText.RankMovedUp(rank.Name),
            FactionRankMove.Down => FactionText.RankMovedDown(rank.Name),
        });
    }

    // By its name alone: Enum.TryParse would take a number for one too.
    private static bool TryPermission(string word, out FactionPermission permission)
    {
        permission = default;
        return word.All(char.IsAsciiLetter) && Enum.TryParse(word, ignoreCase: true, out permission);
    }

    private static bool TrySwitch(string word, out bool on)
    {
        on = string.Equals(word, On, StringComparison.OrdinalIgnoreCase);
        return on || string.Equals(word, Off, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMove(string word, out FactionRankMove move)
    {
        var up = string.Equals(word, Up, StringComparison.OrdinalIgnoreCase);
        move = up ? FactionRankMove.Up : FactionRankMove.Down;
        return up || string.Equals(word, Down, StringComparison.OrdinalIgnoreCase);
    }
}
