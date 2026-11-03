using CyrodiilRP.Data;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;

namespace CyrodiilRP.Tests.Rules;

public sealed class StaffRolesTests
{
    private const string Kick = "akick";
    private const string Mute = "amute";
    private const string Ban = "aban";
    private const string Gone = "aknockout";

    private readonly StaffRoles _roles = new(null!, new RpConfig());

    public StaffRolesTests()
    {
        foreach (var permission in new[] { Kick, Mute, Ban })
            _roles.Declare(permission);

        _roles.Replace([
            CreateOwner("owner"),
            CreateRole("moderator", Kick, Mute),
            CreateRole("helper", Kick, Mute),
            CreateRole("senior", Kick, Mute, Ban),
            CreateRole("banner", Ban),
            CreateRole("old", Kick, Mute, Gone),
        ]);
    }

    [Fact]
    public void A_player_without_a_staff_role_is_outranked_by_any_staff_member()
    {
        Assert.True(_roles.Outranks(CreateStaff("moderator"), []));
        Assert.True(_roles.Outranks(CreateStaff("banner"), ["deleted-role"]));
    }

    [Fact]
    public void A_role_with_every_permission_of_another_and_more_outranks_it_and_never_the_other_way()
    {
        Assert.True(_roles.Outranks(CreateStaff("senior"), ["moderator"]));
        Assert.False(_roles.Outranks(CreateStaff("moderator"), ["senior"]));
    }

    [Fact]
    public void Equal_ranks_and_roles_apart_never_outrank_each_other()
    {
        Assert.False(_roles.Outranks(CreateStaff("moderator"), ["moderator"]));
        Assert.False(_roles.Outranks(CreateStaff("moderator"), ["helper"]));
        Assert.False(_roles.Outranks(CreateStaff("banner"), ["moderator"]));
        Assert.False(_roles.Outranks(CreateStaff("moderator"), ["banner"]));
    }

    [Fact]
    public void A_permission_no_code_declares_makes_no_rank()
    {
        Assert.False(_roles.Outranks(CreateStaff("old"), ["moderator"]));
        Assert.False(_roles.Outranks(CreateStaff("old"), ["helper"]));
    }

    [Fact]
    public void Nobody_punishes_an_owner_and_an_owner_punishes_everyone_else()
    {
        Assert.False(_roles.Outranks(CreateStaff("senior"), ["owner"]));
        Assert.False(_roles.Outranks(CreateStaff("owner"), ["owner"]));
        Assert.True(_roles.Outranks(CreateStaff("owner"), ["senior"]));
    }

    [Fact]
    public void Staff_grant_only_roles_below_their_own_and_nobody_grants_the_owner_role()
    {
        Assert.True(_roles.MayChange(CreateStaff("senior"), "moderator"));
        Assert.False(_roles.MayChange(CreateStaff("moderator"), "helper"));
        Assert.False(_roles.MayChange(CreateStaff("moderator"), "senior"));
        Assert.True(_roles.MayChange(CreateStaff("owner"), "senior"));
        Assert.False(_roles.MayChange(CreateStaff("owner"), "owner"));
    }

    [Fact]
    public void A_role_no_longer_in_the_database_goes_only_by_one_who_holds_every_permission()
    {
        Assert.False(_roles.MayChange(CreateStaff("senior"), "deleted-role"));
        Assert.True(_roles.MayChange(CreateStaff("owner"), "deleted-role"));
    }

    [Fact]
    public void Permissions_come_from_the_roles_held_and_the_owner_role_holds_all()
    {
        Assert.True(_roles.Allows(CreateStaff("moderator"), Kick));
        Assert.False(_roles.Allows(CreateStaff("moderator"), Ban));
        Assert.True(_roles.Allows(CreateStaff("owner"), "anything"));
        Assert.False(_roles.Allows(CreateStaff(), Kick));
    }

    private static OnlinePlayer CreateStaff(params string[] roles) => new(default, Guid.NewGuid(), DateTimeOffset.UtcNow) { Roles = roles };

    private static StaffRole CreateRole(string name, params string[] permissions) => new() { Name = name, Label = name, Permissions = permissions };

    private static StaffRole CreateOwner(string name) => new() { Name = name, Label = name, GrantsAll = true };
}
