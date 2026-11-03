using System.Globalization;

namespace CyrodiilRP.Server.Commands;

internal static class CommandLine
{
    // A year, so a typo of extra zeros does not come out as forever.
    public const int MaxMinutes = 525_600;

    public static bool TryParse(string line, out string name, out string arguments)
    {
        name = string.Empty;
        arguments = string.Empty;
        if (line.Length < 2 || line[0] != '/')
            return false;

        var body = line[1..];
        var space = body.IndexOf(' ');
        name = space < 0 ? body : body[..space];
        arguments = space < 0 ? string.Empty : body[(space + 1)..].Trim();
        return name.Length > 0;
    }

    public static string SkipWords(string arguments, int skip)
    {
        var rest = arguments;
        for (var i = 0; i < skip; i++)
        {
            var space = rest.IndexOf(' ');
            if (space < 0)
                return string.Empty;

            rest = rest[(space + 1)..].TrimStart();
        }

        return rest;
    }

    // A sign and digits only, whatever the culture: "1,000" or "1e3" is no number.
    public static bool TryNumber(string word, out int value) =>
        int.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

    public static bool TryPlayerNumber(string word, out ushort number) =>
        ushort.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    // 0 is until lifted, which comes out as null.
    public static bool TryMinutes(string word, out TimeSpan? duration)
    {
        duration = null;
        if (!int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes > MaxMinutes)
            return false;

        duration = minutes == 0 ? null : TimeSpan.FromMinutes(minutes);
        return true;
    }
}
