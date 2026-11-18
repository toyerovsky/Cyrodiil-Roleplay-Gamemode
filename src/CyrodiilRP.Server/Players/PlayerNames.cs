namespace CyrodiilRP.Server.Players;

// A player named at the start of a command's arguments: by the character's full name, its first name or the ReadyM
// nick. Full names hold a space, so the longest name the text starts with is the one meant.
internal static class PlayerNames
{
    public static NamedPlayer Take(string text, IEnumerable<OnlinePlayer> players)
    {
        var trimmed = text.Trim();
        var matches = players
            .Select(player => new Candidate(player, LongestName(trimmed, player)))
            .Where(candidate => candidate.Name.Length > 0)
            .ToList();
        if (matches.Count == 0)
            return new NamedPlayer(PlayerNameMatch.Unknown, null, string.Empty);

        var longest = matches.Max(candidate => candidate.Name.Length);
        var best = matches.Where(candidate => candidate.Name.Length == longest).ToList();
        return best.Count == 1
            ? new NamedPlayer(PlayerNameMatch.Found, best[0].Player, trimmed[longest..].TrimStart())
            : new NamedPlayer(PlayerNameMatch.Ambiguous, null, string.Empty);
    }

    private static string LongestName(string text, OnlinePlayer player) =>
        Names(player)
            .Where(name => name.Length > 0 && StartsWithName(text, name))
            .MaxBy(name => name.Length) ?? string.Empty;

    private static IEnumerable<string> Names(OnlinePlayer player)
    {
        if (player.Character is { } character)
        {
            yield return character.FullName;
            yield return character.FirstName;
        }

        yield return player.AccountName;
    }

    private static bool StartsWithName(string text, string name) =>
        text.StartsWith(name, StringComparison.OrdinalIgnoreCase) && (text.Length == name.Length || text[name.Length] == ' ');

    private sealed record Candidate(OnlinePlayer Player, string Name);
}
