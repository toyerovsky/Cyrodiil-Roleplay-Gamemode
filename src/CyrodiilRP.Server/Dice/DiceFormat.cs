using System.Globalization;

namespace CyrodiilRP.Server.Dice;

// How a roll reads in the chat, and how the log keeps its dice.
internal static class DiceFormat
{
    // The longest reason a roll made for something else carries.
    public const int MaxReasonLength = 40;

    private const string Joiner = " + ";
    private const string DiceSeparator = ", ";
    private const char StoredSeparator = ',';
    private const char StoredPair = ':';

    // "1d20 + 2d6".
    public static string FormatAsked(IEnumerable<DiceGroup> groups) =>
        string.Join(Joiner, groups.Select(group => $"{group.Count}d{(int)group.Kind}"));

    // "d20 [14], d6 [3]".
    public static string FormatDice(IEnumerable<RolledDie> dice) =>
        string.Join(DiceSeparator, dice.Select(die => $"d{(int)die.Kind} [{die.Value}]"));

    // "20:14,6:3", as the log keeps them.
    public static string Store(IEnumerable<RolledDie> dice) =>
        string.Join(StoredSeparator, dice.Select(die => $"{(int)die.Kind}{StoredPair}{die.Value}"));

    // The log is the server's own: a pair that does not read is a bug.
    public static IReadOnlyList<DieView> Read(string stored) =>
        stored.Length == 0
            ? []
            : stored.Split(StoredSeparator).Select(pair =>
            {
                var parts = pair.Split(StoredPair);
                if (parts.Length != 2
                    || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var sides)
                    || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                    throw new FormatException($"The stored die \"{pair}\" is not sides:value.");

                return new DieView(sides, value);
            }).ToList();

    // Letters, digits, spaces, apostrophes and hyphens, at most 40 of them: a reason another system passes goes into a
    // line every player around reads.
    public static string CleanReason(string? reason)
    {
        var kept = new string((reason ?? string.Empty).Where(c => char.IsLetterOrDigit(c) || c is ' ' or '\'' or '-').ToArray());
        kept = string.Join(' ', kept.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return kept.Length <= MaxReasonLength ? kept : kept[..MaxReasonLength].TrimEnd();
    }
}
