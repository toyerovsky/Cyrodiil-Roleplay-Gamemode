using System.Text.Json;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.PlayerMenu;

namespace CyrodiilRP.Tests.PlayerMenu;

public sealed class HelpListingTests
{
    // The server's locales, copied beside the server half and so beside the tests.
    private static readonly string Locales = Path.Combine(AppContext.BaseDirectory, "locales");

    [Fact]
    public void Headings_come_in_their_order_and_the_commands_by_name_under_each()
    {
        var listing = HelpListing.Build(
        [
            CreateSpec("roll", CommandCategory.Roleplay),
            CreateSpec("players", CommandCategory.General),
            CreateSpec("me", CommandCategory.Roleplay),
            CreateSpec("pm", CommandCategory.Messages),
        ], FormatUsage);

        Assert.Equal(["roleplay", "messages", "general"], listing.Select(group => group.Category));
        Assert.Equal(["/me <x>", "/roll <x>"], listing[0].Commands.Select(command => command.Syntax));
    }

    [Fact]
    public void Staff_commands_come_last_under_their_own_heading()
    {
        var listing = HelpListing.Build([CreateSpec("akick", CommandCategory.Staff), CreateSpec("gold", CommandCategory.General)], FormatUsage);

        Assert.Equal(["general", "staff"], listing.Select(group => group.Category));
    }

    [Fact]
    public void A_usage_is_split_into_the_command_and_what_it_does_at_the_first_separator()
    {
        var command = HelpListing.ParseCommand($"/aroles <number> [roles|-]{HelpListing.Separator}set roles{HelpListing.Separator}or none");

        Assert.Equal("/aroles <number> [roles|-]", command.Syntax);
        Assert.Equal($"set roles{HelpListing.Separator}or none", command.Description);
    }

    [Fact]
    public void A_usage_without_the_separator_is_refused()
    {
        Assert.Throws<FormatException>(() => HelpListing.ParseCommand("/roll a die"));
    }

    // The help splits every command's usage, in whatever language its reader has.
    [Fact]
    public void Every_usage_in_every_language_names_the_command_apart_from_what_it_does()
    {
        var usages = Directory.EnumerateFiles(Locales, "*.json", SearchOption.AllDirectories)
            .SelectMany(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement.EnumerateObject()
                .Where(text => text.Name.Contains(".cmd.", StringComparison.Ordinal))
                .Select(text => $"{Path.GetFileName(Path.GetDirectoryName(file))}/{text.Name}: {text.Value.GetString()}"))
            .ToList();

        Assert.NotEmpty(usages);
        Assert.DoesNotContain(usages, usage => !usage.Contains(HelpListing.Separator, StringComparison.Ordinal));
    }

    private static string FormatUsage(Phrase phrase) => $"/{phrase.Key} <x>{HelpListing.Separator}does {phrase.Key}";

    private static CommandSpec CreateSpec(string name, CommandCategory category) =>
        new() { Name = name, Usage = new Phrase(name), Category = category };
}
