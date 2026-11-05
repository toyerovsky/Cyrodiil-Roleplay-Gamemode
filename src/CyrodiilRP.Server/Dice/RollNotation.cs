using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Dice;

// What follows /roll: nothing (a d20), "20" or "d20", "2d6", "1d20+2d6". The four dice of the board only, five at most.
internal static partial class RollNotation
{
    // As many as the board lays out and the eye still reads.
    public const int MaxDice = 5;

    private const char Plus = '+';

    public static IReadOnlyList<DiceGroup> Default { get; } = [new DiceGroup(DieKind.D20, 1)];

    public static bool TryParse(string arguments, out IReadOnlyList<DiceGroup> groups, [NotNullWhen(false)] out Phrase? refusal)
    {
        groups = Default;
        refusal = null;
        var trimmed = arguments.Trim();
        if (trimmed.Length == 0)
            return true;

        List<DiceGroup> parsed = [];
        var total = 0;
        foreach (var part in trimmed.Split(Plus))
        {
            var term = part.Trim();
            if (parsed.Count > 0 && NumberTerm().IsMatch(term))
            {
                refusal = DiceText.NoModifiers;
                return false;
            }

            if (DiceTerm().Match(term) is not { Success: true } match)
            {
                refusal = DiceText.Usage(MaxDice);
                return false;
            }

            var countText = match.Groups["count"].Value;
            var count = 1;
            // More digits than a number holds is more dice than the board does.
            if (countText.Length > 0 && !int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out count))
            {
                refusal = DiceText.TooMany(MaxDice);
                return false;
            }

            if (count < 1)
            {
                refusal = DiceText.Usage(MaxDice);
                return false;
            }

            total += count;
            if (total > MaxDice)
            {
                refusal = DiceText.TooMany(MaxDice);
                return false;
            }

            var sides = int.Parse(match.Groups["sides"].Value, CultureInfo.InvariantCulture);
            parsed.Add(new DiceGroup((DieKind)sides, count));
        }

        groups = parsed;
        return true;
    }

    // "20" alone is a d20, as /roll took a number of sides before the board came.
    [GeneratedRegex(@"^(?:(?<count>\d*)d)?(?<sides>6|10|20|100)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiceTerm();

    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberTerm();
}
