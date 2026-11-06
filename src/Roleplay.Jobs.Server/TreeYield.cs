namespace Roleplay.Jobs.Server;

/// <summary>
/// What a tree takes and gives, by its kind and its size. A tree is its model scaled the same on every axis, so its
/// trunk's thickness grows with its scale and what it gives with a power of it (<see cref="TreeDrop.ScaleExponent"/>).
/// </summary>
public static class TreeYield
{
    /// <summary>The first kind whose match is in the tree's mesh name, ignoring case, else the default kind.</summary>
    public static TreeKind KindFor(string treeMesh, WoodcuttingConfig config)
        => config.Kinds.FirstOrDefault(kind => kind.Match.Length > 0 && Names(treeMesh, kind.Match)) ?? config.DefaultKind;

    /// <summary>
    /// The damage that fells it: more for a thicker trunk, kept between the limits, then more again for every tier the
    /// axe falls short of the wood (TierPenalty to the power of the shortfall, past MaxHealth on purpose). An axe above
    /// the wood leaves it as it is and hits harder instead (<see cref="HitDamage.DamageOf"/>).
    /// </summary>
    /// <param name="axeTier">The tier of the axe it is chopped with, or null when chopping needs no axe.</param>
    public static int HealthFor(TreeKind kind, float treeScale, int? axeTier, WoodcuttingConfig config)
    {
        var health = Math.Clamp((int)MathF.Round(config.BaseHealth * kind.Girth * treeScale, MidpointRounding.AwayFromZero), config.MinHealth, config.MaxHealth);
        return axeTier is { } tier && tier < kind.Tier
            ? (int)Math.Ceiling(health * Math.Pow(config.TierPenalty, kind.Tier - tier))
            : health;
    }

    /// <summary>
    /// How many of a drop a felled tree gives, 0 when it gives none: when the drop is for other kinds of tree, or its
    /// chance roll fails. The fraction of the expected count is the chance of one more.
    /// </summary>
    /// <param name="roll">Uniform draws in [0, 1): one for the chance, then one for the fraction.</param>
    public static int CountFor(TreeDrop drop, string treeMesh, TreeKind kind, float treeScale, Func<double> roll)
    {
        if (drop.Kinds.Count > 0 && !drop.Kinds.Any(match => Names(treeMesh, match)))
            return 0;
        if (roll() >= drop.Chance)
            return 0;

        var expected = drop.Base * (drop.ScalesWithWood ? kind.Wood : 1f) * Math.Pow(treeScale, drop.ScaleExponent);
        var whole = Math.Floor(expected);
        var count = (int)whole + (roll() < expected - whole ? 1 : 0);
        return Math.Clamp(count, drop.Min, drop.Max);
    }

    private static bool Names(string treeMesh, string match)
        => match.Length > 0 && treeMesh.Contains(match, StringComparison.OrdinalIgnoreCase);
}
