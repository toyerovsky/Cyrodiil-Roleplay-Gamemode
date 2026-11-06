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

    /// <summary>A spent node's yield shared by hits, the best of it to whoever struck most: <see cref="YieldShare.Share"/>.</summary>
    public static Dictionary<TPlayer, List<OreDrop>> Share<TPlayer>(IReadOnlyList<OreDrop> items, IReadOnlyList<(TPlayer Player, int Hits)> hits)
        where TPlayer : notnull
        => YieldShare.Share(items, item => item.Value, hits);

    /// <summary>A node's id: its kind and where it stands, in whole centimeters, the same on every run.</summary>
    public static string NodeId(OreNode node) =>
        $"jobs/ore/{node.Kind}|{(int)MathF.Round(node.X)}|{(int)MathF.Round(node.Y)}|{(int)MathF.Round(node.Z)}";

    public static Vector3 PositionOf(OreNode node) => new(node.X, node.Y, node.Z);
}
