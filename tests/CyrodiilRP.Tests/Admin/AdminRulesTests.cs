using System.Reflection;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Admin;

namespace CyrodiilRP.Tests.Admin;

public sealed class AdminRulesTests
{
    [Theory]
    [InlineData("0", 0f)]
    [InlineData("13", 13f)]
    [InlineData("13:30", 13.5f)]
    [InlineData("23:59", 23f + 59f / 60f)]
    public void An_hour_is_taken_whole_or_with_its_minutes(string text, float hour)
    {
        Assert.True(WorldCommands.TryHour(text, out var parsed));
        Assert.Equal(hour, parsed, 0.0001f);
    }

    [Theory]
    [InlineData("24")]
    [InlineData("7:60")]
    [InlineData("-1")]
    [InlineData("noon")]
    [InlineData("1:2:3")]
    public void Anything_else_is_no_hour(string text)
    {
        Assert.False(WorldCommands.TryHour(text, out _));
    }

    [Theory]
    [InlineData(23.8f, 12f, 4f)]
    [InlineData(1.5f, 13f, 3f)]
    [InlineData(12f, 12f, 3f)]
    public void An_hour_already_gone_by_is_the_next_days(float now, float hour, float daysPassed)
    {
        Assert.Equal(daysPassed, WorldCommands.DaysPassedAt(3f, now, hour));
    }

    // A button runs a command the registry knows by a permission, so its checks and the audit are the command's own.
    [Fact]
    public void Every_button_runs_a_staff_command()
    {
        var declared = typeof(StaffPermissions).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetRawConstantValue()!);

        Assert.Subset(declared.ToHashSet(), AdminActions.Commands.ToHashSet());
        Assert.Equal(AdminActions.Commands.Count, AdminActions.Commands.Distinct().Count());
    }

    [Fact]
    public void Players_the_audit_the_rolls_and_the_factions_show_to_every_holder_of_the_panel_and_the_rest_only_with_something_to_use()
    {
        Assert.Equal([AdminTabs.Players, AdminTabs.Audit, AdminTabs.Dice, AdminTabs.Factions], AdminActions.ListTabs(_ => false));
        Assert.Equal([AdminTabs.Players, AdminTabs.Sanctions, AdminTabs.Audit, AdminTabs.Dice, AdminTabs.Factions],
            AdminActions.ListTabs(name => name == StaffPermissions.Unmute));
        Assert.Equal([AdminTabs.Players, AdminTabs.Audit, AdminTabs.World, AdminTabs.Dice, AdminTabs.Factions],
            AdminActions.ListTabs(name => name == StaffPermissions.Teleport));
    }
}
