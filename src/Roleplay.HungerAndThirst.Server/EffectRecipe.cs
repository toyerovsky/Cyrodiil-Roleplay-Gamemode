using OblivionMP.SDK.Common.Statuses;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>One effect of a tier, read from the config: what it does and how strong.</summary>
/// <param name="Kind">Fortify, Drain, Damage or Restore on <paramref name="Stat"/>, or null for a coded effect.</param>
/// <param name="Stat">The stat it acts on, with <paramref name="Kind"/>.</param>
/// <param name="Code">The four letter code of an effect that names no stat (Burden is BRDN), without a kind.</param>
/// <param name="Points">Magnitude, per second for Damage and Restore.</param>
/// <param name="Percent">Magnitude as a share of the character's own maximum, for Health, Magicka and Fatigue.</param>
public readonly record struct EffectRecipe(StatusKind? Kind, Stat? Stat, string? Code, int Points, float Percent)
{
    /// <summary>The vital it acts on, or null for anything else.</summary>
    public Stat? Vital => Stat is { } stat && stat.Group() == StatGroup.Vital ? stat : null;
}
