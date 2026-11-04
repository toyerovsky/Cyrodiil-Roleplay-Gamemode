using System.Numerics;

namespace Roleplay.Jobs.Server;

/// <summary>What a node takes and gives, and what it is called. Pure, so the server and the tests agree.</summary>
public static class OreYield
{
    /// <summary>The hits a node takes: its kind's, times its scale, then by the warhammer's tier against the kind's.</summary>
    /// <param name="hammerTier">Null when no warhammer is needed.</param>
    public static int HitsFor(OreKind kind, float scale, int? hammerTier, MiningConfig config)
    {
        var hits = Math.Max(1, (int)MathF.Round(kind.Hits * scale, MidpointRounding.AwayFromZero));
        if (hammerTier is not { } tier || tier == kind.Tier)
            return hits;

        return tier < kind.Tier
            ? (int)Math.Ceiling(hits * Math.Pow(config.TierPenalty, kind.Tier - tier))
            : Math.Max(1, (int)Math.Round(hits * Math.Pow(config.TierBonus, tier - kind.Tier), MidpointRounding.AwayFromZero));
    }

    /// <summary>How many of the drop a node of this scale gives: 0 when its chance roll fails.</summary>
    /// <param name="roll">Uniform in [0, 1): one roll for the chance, one for a fraction's extra one.</param>
    public static int CountFor(OreDrop drop, float scale, Func<double> roll)
    {
        if (drop.Chance < 1f && roll() >= drop.Chance)
            return 0;

        var amount = drop.Base * scale;
        var whole = (int)MathF.Floor(amount);
        if (roll() < amount - whole)
            whole++;
        return Math.Clamp(whole, drop.Min, drop.Max);
    }

    /// <summary>
    /// Shares a spent node's yield, one entry per item, between the players who hit it, by their hits: the items go most
    /// valuable first, each to whoever is furthest short of their share of the whole. A share counts an item as its
    /// value plus one, so worthless items still divide by count. Whoever did most of the work gets the best of it; one
    /// hit among many gets what is left, maybe nothing.
    /// </summary>
    /// <param name="hits">Each player's hits; ties go to more hits, then to the earlier in the list.</param>
    public static Dictionary<TPlayer, List<OreDrop>> Share<TPlayer>(IReadOnlyList<OreDrop> items, IReadOnlyList<(TPlayer Player, int Hits)> hits)
        where TPlayer : notnull
    {
        var shares = hits.Where(entry => entry.Hits > 0).ToList();
        var given = shares.ToDictionary(entry => entry.Player, _ => new List<OreDrop>());
        if (shares.Count == 0)
            return given;

        var allHits = (double)shares.Sum(entry => entry.Hits);
        var whole = items.Sum(item => (double)Weight(item));
        var owed = shares.Select(entry => whole * entry.Hits / allHits).ToArray();
        foreach (var item in items.OrderByDescending(item => item.Value))
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

    private static int Weight(OreDrop item) => Math.Max(0, item.Value) + 1;

    /// <summary>A node's id: its kind and where it stands, in whole centimeters, the same on every run.</summary>
    public static string NodeId(OreNode node) =>
        $"jobs/ore/{node.Kind}|{(int)MathF.Round(node.X)}|{(int)MathF.Round(node.Y)}|{(int)MathF.Round(node.Z)}";

    public static Vector3 PositionOf(OreNode node) => new(node.X, node.Y, node.Z);
}
