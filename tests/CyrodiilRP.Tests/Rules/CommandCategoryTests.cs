using System.Reflection;
using System.Runtime.CompilerServices;
using CyrodiilRP.Data;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Rules;

public sealed class CommandCategoryTests
{
    private const string Command = "atest";
    private const string Moderator = "moderator";

    // So a command never lands in the menu's help without a heading: the compiler refuses a spec without one.
    [Fact]
    public void A_command_cannot_be_given_without_its_heading()
    {
        var category = typeof(CommandSpec).GetProperty(nameof(CommandSpec.Category))!;

        Assert.NotNull(category.GetCustomAttribute<RequiredMemberAttribute>());
    }

    [Fact]
    public void A_staff_command_lands_under_the_staff_and_only_its_permission_lists_it()
    {
        var roles = new StaffRoles(null!, new RpConfig());
        roles.Replace([new StaffRole { Name = Moderator, Label = Moderator, Permissions = [Command] }]);
        var commands = new CommandRegistry(null!, roles, null!, new ListLogger());

        commands.AddStaff(Command, new Phrase($"test.{Command}"), _ => { });

        var spec = Assert.Single(commands.ListAvailable(CreatePlayer(Moderator)));
        Assert.Equal(CommandCategory.Staff, spec.Category);
        Assert.True(spec.Restricted);
        Assert.Empty(commands.ListAvailable(CreatePlayer()));
    }

    private static OnlinePlayer CreatePlayer(params string[] roles) => new(default, Guid.NewGuid(), DateTimeOffset.UtcNow) { IsLoaded = true, Roles = roles };
}
