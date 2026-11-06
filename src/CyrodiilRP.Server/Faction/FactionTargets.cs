using System.Globalization;
using CyrodiilRP.Server.Commands;

namespace CyrodiilRP.Server.Faction;

// A faction, a rank or a member named at the start of a command's arguments. Their names hold spaces, so the longest
// name the text starts with is the one meant, and "#12" names one by its number, as the pages do.
internal static class FactionTargets
{
    public const char NumberMark = '#';

    // Null when the text starts with none of them. Rest: what follows the name.
    public static T? Take<T>(string text, IEnumerable<T> items, Func<T, long> number, Func<T, string> name, out string rest)
        where T : class
    {
        rest = string.Empty;
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return null;

        if (trimmed[0] == NumberMark)
        {
            var space = trimmed.IndexOf(' ');
            var word = space < 0 ? trimmed[1..] : trimmed[1..space];
            rest = space < 0 ? string.Empty : trimmed[(space + 1)..].TrimStart();
            return long.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var wanted)
                ? items.FirstOrDefault(item => number(item) == wanted)
                : null;
        }

        var found = items
            .Where(item => StartsWithName(trimmed, name(item)))
            .MaxBy(item => name(item).Length);
        if (found is not null)
            rest = trimmed[name(found).Length..].TrimStart();

        return found;
    }

    public static FactionRankRecord? TakeRank(FactionRecord faction, string text, out string rest) =>
        Take(text, faction.Ranks, rank => rank.Number, rank => rank.Name, out rest);

    // Also by the number of a player in the game, as every other command names one. FindCharacter: that player's character.
    public static FactionMemberRecord? TakeMember(FactionRecord faction, Func<ushort, long?> findCharacter, string text, out string rest)
    {
        var trimmed = text.Trim();
        var space = trimmed.IndexOf(' ');
        var first = space < 0 ? trimmed : trimmed[..space];
        if (!CommandLine.TryPlayerNumber(first, out var number))
            return Take(trimmed, faction.Members, member => member.CharacterId, member => member.Name, out rest);

        rest = space < 0 ? string.Empty : trimmed[(space + 1)..].TrimStart();
        return findCharacter(number) is { } characterId ? faction.FindMember(characterId) : null;
    }

    private static bool StartsWithName(string text, string name) =>
        text.StartsWith(name, StringComparison.OrdinalIgnoreCase) && (text.Length == name.Length || text[name.Length] == ' ');
}
