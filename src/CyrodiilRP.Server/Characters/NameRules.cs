using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Characters;

internal static partial class NameRules
{
    public const int MinLength = 2;

    public const int MaxLength = 20;

    // Words that would make a character pass for staff, or for the server speaking.
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "moderator", "mod", "support", "staff", "gm", "owner", "server", "readym", "ooc",
    };

    // A capital at the start and after a hyphen (Gro-Malog), not after an apostrophe (Ra'jirr).
    public static string Normalize(string name)
    {
        var lower = name.Trim().ToLower(CultureInfo.InvariantCulture);
        var built = new StringBuilder(lower.Length);

        for (var i = 0; i < lower.Length; i++)
            built.Append(i == 0 || lower[i - 1] == '-' ? char.ToUpper(lower[i], CultureInfo.InvariantCulture) : lower[i]);

        return built.ToString();
    }

    public static Phrase? Check(string name, Phrase part)
    {
        if (name.Length is < MinLength or > MaxLength)
            return CharactersText.NameLength(part, MinLength, MaxLength);

        if (!Shape.IsMatch(name))
            return CharactersText.NameShape(part);

        return Reserved.Contains(name) ? CharactersText.NameReserved(part) : null;
    }

    // Latin letters only: a Cyrillic "a" in "Marius" would pass for another character's name.
    [GeneratedRegex(@"^[\p{L}-[^A-Za-z\u00C0-\u024F]]+(?:['-][\p{L}-[^A-Za-z\u00C0-\u024F]]+)*$")]
    private static partial Regex Shape { get; }
}
