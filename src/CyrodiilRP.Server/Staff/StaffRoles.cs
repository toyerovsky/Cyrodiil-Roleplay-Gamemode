using CyrodiilRP.Data;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Staff;

// Permissions are looked up through the roles at every check.
[Service]
internal sealed partial class StaffRoles(Persistence persistence, RpConfig config)
{
    private readonly Dictionary<string, StaffRole> _roles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _declared = new(StringComparer.Ordinal);

    public IEnumerable<string> RoleNames => _roles.Keys.Order(StringComparer.Ordinal);

    // Once, as the mod starts, right after the schema is known to fit.
    public void Load()
    {
        using var db = persistence.Factory.Create();
        Replace(StaffBook.LoadRolesAsync(db, CancellationToken.None).GetAwaiter().GetResult());
    }

    public void Replace(IEnumerable<StaffRole> roles)
    {
        _roles.Clear();
        foreach (var role in roles)
            _roles[role.Name] = role;
    }

    // The permission of a staff command, which is its name.
    public void Declare(string permission) => _declared.Add(permission);

    public bool IsKnown(string role) => _roles.ContainsKey(role);

    // The owners of the server config hold the owner role on top of what the database gives them, online or not.
    public string[] ResolveRoles(Guid readyMId, string[] stored) =>
        config.Owners.Contains(readyMId) && !stored.Contains(StaffRole.Owner) ? [.. stored, StaffRole.Owner] : stored;

    public bool Allows(OnlinePlayer player, string permission) => Allows(player.Roles, permission);

    // On duty, or holding every permission: the owners act without it, as they answer for the server itself.
    public bool Acts(OnlinePlayer player) => player.DutySince is not null || GrantsAll(player.Roles);

    // Whom the staff may punish: one whose roles give no staff permission, or one they strictly outrank, holding every
    // staff permission the target holds and more. Equals never punish each other, and nobody punishes an owner.
    public bool Outranks(OnlinePlayer actor, IEnumerable<string> targetRoles) => IsAbove(actor.Roles, FindKnown(targetRoles).ToList());

    // A role no longer in the database goes only by one who holds every permission.
    public bool MayChange(OnlinePlayer actor, string role) =>
        _roles.GetValueOrDefault(role) is { } known ? CanGrant(actor.Roles, known) : GrantsAll(actor.Roles);

    private bool Allows(IEnumerable<string> roles, string permission) =>
        roles.Any(name => _roles.TryGetValue(name, out var role) && (role.GrantsAll || role.Permissions.Contains(permission)));

    private bool GrantsAll(IEnumerable<string> roles) => roles.Any(name => _roles.TryGetValue(name, out var role) && role.GrantsAll);

    // Nobody hands out more than they hold, nor a role of their own rank. The role with every permission goes by no
    // command at all, only to the owners in the server config, so nobody makes a new owner from the chat.
    private bool CanGrant(IReadOnlyCollection<string> actorRoles, StaffRole role) =>
        !role.GrantsAll && (GrantsAll(actorRoles) || (role.Permissions.All(p => Allows(actorRoles, p)) && IsAbove(actorRoles, [role])));

    private bool IsAbove(IEnumerable<string> actorRoles, IReadOnlyCollection<StaffRole> target)
    {
        if (target.Any(role => role.GrantsAll))
            return false;

        var held = CollectPermissions(target);
        if (held.Count == 0 || GrantsAll(actorRoles))
            return true;

        return CollectPermissions(FindKnown(actorRoles)).IsProperSupersetOf(held);
    }

    private IEnumerable<StaffRole> FindKnown(IEnumerable<string> names) => names.Select(name => _roles.GetValueOrDefault(name)).OfType<StaffRole>();

    // A permission no code declares opens nothing, so it makes no rank either.
    private HashSet<string> CollectPermissions(IEnumerable<StaffRole> roles) =>
        roles.SelectMany(role => role.Permissions).Where(_declared.Contains).ToHashSet(StringComparer.Ordinal);
}
