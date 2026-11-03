using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.PlayerMenu;

internal static class HelpListing
{
    // Between a command's syntax and what it does, in the usage texts of every language.
    public const string Separator = " - ";

    public static IReadOnlyList<MenuHelpGroupView> Build(IEnumerable<CommandSpec> commands, Func<Phrase, string> text) =>
        commands
            .GroupBy(spec => spec.Category)
            .OrderBy(group => group.Key)
            .Select(group => new MenuHelpGroupView(
                FormatCategory(group.Key),
                group.OrderBy(spec => spec.Name, StringComparer.Ordinal).Select(spec => ParseCommand(text(spec.Usage))).ToList()))
            .ToList();

    public static MenuCommandView ParseCommand(string usage)
    {
        var at = usage.IndexOf(Separator, StringComparison.Ordinal);
        if (at < 0)
            throw new FormatException($"The usage \"{usage}\" has no \"{Separator}\" between the command and what it does.");

        return new MenuCommandView(usage[..at], usage[(at + Separator.Length)..]);
    }

    // As the page names the heading: ui.menu.category.<id>.
    private static string FormatCategory(CommandCategory category) => category.ToString().ToLowerInvariant();
}
