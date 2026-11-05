using System.Globalization;
using System.Text.RegularExpressions;

namespace CyrodiilRP.Server.Lore;

// A trait's rolls as the database keeps them: "5d20:3,17,9,11,6=46;5d20:...".
internal static partial class LoreRollFormat
{
    private const char RollSeparator = ';';
    private const char DieSeparator = ',';

    public static string? Store(IReadOnlyList<LoreTraitRoll> rolls) =>
        rolls.Count == 0
            ? null
            : string.Join(RollSeparator, rolls.Select(roll =>
                $"{roll.Dice.Count}d20:{string.Join(DieSeparator, roll.Dice.Select(die => die.ToString(CultureInfo.InvariantCulture)))}={roll.Total.ToString(CultureInfo.InvariantCulture)}"));

    // The column is the server's own: a roll that does not read is a bug.
    public static IReadOnlyList<LoreTraitRoll> Read(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return [];

        return stored.Split(RollSeparator).Select(part =>
        {
            if (StoredRoll().Match(part) is not { Success: true } match)
                throw new FormatException($"The stored trait roll \"{part}\" does not read.");

            var dice = match.Groups["dice"].Value.Split(DieSeparator).Select(die => int.Parse(die, CultureInfo.InvariantCulture)).ToList();
            var total = int.Parse(match.Groups["total"].Value, CultureInfo.InvariantCulture);
            if (dice.Count != int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture) || dice.Sum() != total)
                throw new FormatException($"The stored trait roll \"{part}\" does not add up.");

            return new LoreTraitRoll(dice, total);
        }).ToList();
    }

    [GeneratedRegex(@"^(?<count>\d+)d20:(?<dice>\d+(?:,\d+)*)=(?<total>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex StoredRoll();
}
