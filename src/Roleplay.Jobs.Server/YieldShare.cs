namespace Roleplay.Jobs.Server;

/// <summary>
/// Shares what a spent node or a felled tree gives among the players who hit it. Pure, so the server and the tests agree.
/// </summary>
public static class YieldShare
{
    /// <summary>
    /// Shares the yield, one entry per item, between the players who hit it, by their hits: the items go most valuable
    /// first, each to whoever is furthest short of their share of the whole. A share counts an item as its value plus
    /// one, so worthless items still divide by count. Whoever did most of the work gets the best of it; one hit among
    /// many gets what is left, maybe nothing.
    /// </summary>
    /// <param name="value">An item's gold value; below 0 counts as 0.</param>
    /// <param name="hits">Each player's hits; ties go to more hits, then to the earlier in the list.</param>
    public static Dictionary<TPlayer, List<TItem>> Share<TItem, TPlayer>(IReadOnlyList<TItem> items, Func<TItem, int> value,
        IReadOnlyList<(TPlayer Player, int Hits)> hits)
        where TPlayer : notnull
    {
        var shares = hits.Where(entry => entry.Hits > 0).ToList();
        var given = shares.ToDictionary(entry => entry.Player, _ => new List<TItem>());
        if (shares.Count == 0)
            return given;

        int Weight(TItem item) => Math.Max(0, value(item)) + 1;

        var allHits = (double)shares.Sum(entry => entry.Hits);
        var whole = items.Sum(item => (double)Weight(item));
        var owed = shares.Select(entry => whole * entry.Hits / allHits).ToArray();
        foreach (var item in items.OrderByDescending(value))
        {
            var taker = 0;
            for (var i = 1; i < shares.Count; i++)
            {
                if (owed[i] > owed[taker] || (owed[i] == owed[taker] && shares[i].Hits > shares[taker].Hits))
                    taker = i;
            }
            given[shares[taker].Player].Add(item);
            owed[taker] -= Weight(item);
        }
        return given;
    }
}

/// <summary>Every player's hits on one tree or node, in the order they first struck, and when the last one landed. A hit
/// can count for more than one (a power attack's damage on a tree).</summary>
public sealed class HitTally<TPlayer> where TPlayer : notnull
{
    private readonly List<(TPlayer Player, int Hits)> _byPlayer = [];

    public IReadOnlyList<(TPlayer Player, int Hits)> ByPlayer => _byPlayer;

    public double LastHitAt { get; private set; }

    public int Hits => _byPlayer.Sum(entry => entry.Hits);

    public void Add(TPlayer player, double at, int amount = 1)
    {
        LastHitAt = at;
        var index = _byPlayer.FindIndex(entry => EqualityComparer<TPlayer>.Default.Equals(entry.Player, player));
        if (index < 0)
            _byPlayer.Add((player, amount));
        else
            _byPlayer[index] = (player, _byPlayer[index].Hits + amount);
    }

    public override string ToString() => string.Join(", ", _byPlayer.Select(entry => $"{entry.Player}: {entry.Hits}"));
}
