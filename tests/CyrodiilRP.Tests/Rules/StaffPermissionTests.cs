using System.Reflection;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Rules;

public sealed class StaffPermissionTests
{
    private static readonly Dictionary<string, string> Declared = typeof(StaffPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);

    // A permission no command needs opens nothing, and a seeded role would hold it for nothing.
    [Fact]
    public void Every_permission_is_the_name_of_a_restricted_command()
    {
        var server = string.Concat(SourceFiles.In("src/CyrodiilRP.Server", "*.cs").Select(File.ReadAllText));

        Assert.DoesNotContain(Declared.Keys, field => !server.Contains($"StaffPermissions.{field},", StringComparison.Ordinal)
                                                      && !server.Contains($"Name = StaffPermissions.{field}", StringComparison.Ordinal));
    }

    [Fact]
    public void An_administrator_holds_every_permission_and_a_moderator_part_of_them()
    {
        var admin = StaffRoleSeeds.All.Single(role => role.Name == StaffRole.Admin).Permissions;
        var moderator = StaffRoleSeeds.All.Single(role => role.Name == StaffRole.Moderator).Permissions;

        Assert.Equal(Declared.Values.Order(StringComparer.Ordinal), admin.Order(StringComparer.Ordinal));
        Assert.True(moderator.ToHashSet().IsProperSubsetOf(admin));
    }

    [Fact]
    public void Permissions_are_distinct()
    {
        Assert.Equal(Declared.Count, Declared.Values.Distinct().Count());
        Assert.All(StaffRoleSeeds.All, role => Assert.Equal(role.Permissions.Length, role.Permissions.Distinct().Count()));
    }
}
