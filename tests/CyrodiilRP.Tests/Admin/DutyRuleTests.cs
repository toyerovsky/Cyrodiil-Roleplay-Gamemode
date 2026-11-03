using CyrodiilRP.Data;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Admin;

public sealed class DutyRuleTests
{
    private const string Acting = "aact";
    private const string Showing = "ashow";
    private const string Moderator = "moderator";
    private const string Owner = "owner";

    private readonly StaffRoles _roles = new(null!, new RpConfig());
    private readonly CommandRegistry _commands;
    private readonly RecordedOutput _output = new();
    private readonly List<string> _ran = [];

    public DutyRuleTests()
    {
        _commands = new CommandRegistry(null!, _roles, null!, new ListLogger());
        _commands.AddStaff(Acting, new Phrase($"test.{Acting}"), _ => _ran.Add(Acting));
        _commands.AddStaff(Showing, new Phrase($"test.{Showing}"), _ => _ran.Add(Showing), offDuty: true);
        _roles.Replace(
        [
            new StaffRole { Name = Moderator, Label = Moderator, Permissions = [Acting, Showing] },
            new StaffRole { Name = Owner, Label = Owner, GrantsAll = true },
        ]);
    }

    [Fact]
    public void Off_duty_a_staff_command_is_refused_with_the_way_onto_duty()
    {
        _commands.Run(CreateStaff(Moderator), $"/{Acting}", _output);

        Assert.Empty(_ran);
        Assert.Equal([StaffText.OffDuty.Key], _output.Refusals);
    }

    [Fact]
    public void On_duty_it_runs()
    {
        var staff = CreateStaff(Moderator);
        staff.DutySince = DateTimeOffset.UtcNow;

        _commands.Run(staff, $"/{Acting}", _output);

        Assert.Equal([Acting], _ran);
    }

    [Fact]
    public void A_command_that_only_shows_runs_off_duty()
    {
        _commands.Run(CreateStaff(Moderator), $"/{Showing}", _output);

        Assert.Equal([Showing], _ran);
    }

    [Fact]
    public void The_role_with_every_permission_acts_without_duty()
    {
        _commands.Run(CreateStaff(Owner), $"/{Acting}", _output);

        Assert.Equal([Acting], _ran);
    }

    [Fact]
    public void Without_the_permission_the_command_is_still_unknown_on_duty_or_off()
    {
        var player = CreateStaff();
        player.DutySince = DateTimeOffset.UtcNow;

        _commands.Run(player, $"/{Acting}", _output);

        Assert.Empty(_ran);
        Assert.Equal([CommandsText.Unknown(Acting).Key], _output.Refusals);
    }

    private static OnlinePlayer CreateStaff(params string[] roles) => new(default, Guid.NewGuid(), DateTimeOffset.UtcNow) { IsLoaded = true, Roles = roles };
}
