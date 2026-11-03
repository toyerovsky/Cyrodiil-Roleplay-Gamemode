namespace CyrodiilRP.Server.Admin;

// A name or a part that more than one thing has is not a pick: the staff choose among the candidates.
internal static class CatalogLookup
{
    public const int MostCandidates = 20;

    public static Found<T> Find<T>(IReadOnlyList<T> all, string text, Func<T, string> id, Func<T, string> name) where T : class
    {
        var wanted = text.Trim();
        if (wanted.Length == 0)
            return new Found<T>(null, []);

        if (all.FirstOrDefault(each => id(each).Equals(wanted, StringComparison.OrdinalIgnoreCase)) is { } byId)
            return new Found<T>(byId, [byId]);

        var named = all.Where(each => name(each).Equals(wanted, StringComparison.OrdinalIgnoreCase)).Take(MostCandidates).ToList();
        if (named.Count > 0)
            return new Found<T>(named.Count == 1 ? named[0] : null, named);

        var containing = FindContaining(all, wanted, id, name).Take(MostCandidates).ToList();
        return new Found<T>(containing.Count == 1 ? containing[0] : null, containing);
    }

    public static IEnumerable<T> FindContaining<T>(IReadOnlyList<T> all, string text, Func<T, string> id, Func<T, string> name) =>
        all.Where(each => name(each).Contains(text, StringComparison.OrdinalIgnoreCase) || id(each).Contains(text, StringComparison.OrdinalIgnoreCase));
}
