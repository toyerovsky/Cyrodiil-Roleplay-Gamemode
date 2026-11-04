using OblivionMp.Sdk.Common.Statuses;
using Roleplay.HungerAndThirst.Common;

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

/// <summary>A tier ready to use: below <paramref name="Below"/> points (0: at empty) the character has the effects.</summary>
public sealed record Tier(string Name, int Below, EffectRecipe[] Effects);

/// <summary>A status the character should have, and the vital it acts on, if any.</summary>
public readonly record struct WantedStatus(StatusEffect Effect, Stat? Vital);

/// <summary>The character's own maximum of each vital, before any of the mod's effects.</summary>
public readonly record struct NaturalVitals(float Health, float Magicka, float Fatigue)
{
    public float Of(Stat vital) => vital switch
    {
        Stat.Health => Health,
        Stat.Magicka => Magicka,
        _ => Fatigue,
    };
}

/// <summary>
/// Turns the hunger and thirst tiers of the config into the statuses a character should have, with no SDK calls, so
/// the tests can run it. Only the deepest tier a meter is in counts, so each tier lists all of its effects.
/// </summary>
public static class SustenanceEffects
{
    // Effects that name no stat, by a friendlier name than their code.
    private static readonly Dictionary<string, string> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Burden"] = "BRDN",
        ["Feather"] = "FTHR",
    };

    /// <summary>
    /// Reads one effect: "&lt;Kind&gt; &lt;Stat&gt;" ("Drain Fatigue", "Damage Health", "Drain Hand to Hand"),
    /// "Burden", "Feather", or any four letter effect code. False with the reason when it is none of these.
    /// </summary>
    public static bool TryRead(TierEffect entry, out EffectRecipe recipe, out string problem)
    {
        recipe = default;
        problem = string.Empty;
        string text = entry.Effect.Trim();
        if (entry.Points < 0 || entry.Percent < 0)
        {
            problem = $"'{text}' has a negative magnitude.";
            return false;
        }

        string[] words = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 2
            && Enum.TryParse(words[0], ignoreCase: true, out StatusKind kind) && Enum.IsDefined(kind)
            && Enum.TryParse(words[1].Replace(" ", ""), ignoreCase: true, out Stat stat) && Enum.IsDefined(stat))
        {
            try
            {
                _ = StatusEffect.Of(kind, stat, 1); // refuses what the game has no effect for, such as Damage Blade
            }
            catch (ArgumentException exception)
            {
                problem = exception.Message;
                return false;
            }
            if (entry.Percent > 0 && stat.Group() != StatGroup.Vital)
            {
                problem = $"'{text}': a percent only works on Health, Magicka or Fatigue. Use points.";
                return false;
            }
            recipe = new EffectRecipe(kind, stat, null, entry.Points, entry.Percent);
            return true;
        }

        string code = Codes.GetValueOrDefault(text, text);
        try
        {
            _ = StatusEffect.Encode(code);
        }
        catch (ArgumentException)
        {
            problem = $"'{text}' is neither \"<Kind> <Stat>\" (Drain Fatigue), Burden, Feather nor a four letter effect code.";
            return false;
        }
        if (entry.Percent > 0)
        {
            problem = $"'{text}': a percent only works on Health, Magicka or Fatigue. Use points.";
            return false;
        }
        recipe = new EffectRecipe(null, null, code.ToUpperInvariant(), entry.Points, 0f);
        return true;
    }

    /// <summary>The tiers of one meter, deepest first. An effect that can't be read is left out and reported.</summary>
    public static Tier[] Resolve(IEnumerable<SustenanceTier> tiers, Action<string> problem)
    {
        List<Tier> resolved = [];
        foreach (SustenanceTier tier in tiers)
        {
            List<EffectRecipe> effects = [];
            foreach (TierEffect entry in tier.Effects)
            {
                if (TryRead(entry, out EffectRecipe recipe, out string why))
                    effects.Add(recipe);
                else
                    problem($"tier \"{tier.Name}\": {why}");
            }
            string name = string.IsNullOrWhiteSpace(tier.Name) ? "Hunger and thirst" : tier.Name.Trim();
            int below = (int)Math.Round(Math.Clamp(tier.BelowPercent, 0f, 100f) / 100f * SustenanceScale.Max);
            resolved.Add(new Tier(name, below, [.. effects]));
        }
        return [.. resolved.OrderBy(tier => tier.Below)];
    }

    /// <summary>
    /// Which tier a meter showing <paramref name="shown"/> is in: the index of the deepest that applies, or -1 for
    /// none. A tier applies below its line, and the one at 0 only once the meter is empty.
    /// </summary>
    public static int TierAt(Tier[] tiers, int shown)
    {
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].Below <= 0 ? shown <= 0 : shown < tiers[i].Below)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// The vital an attribute's drain lowers too: Strength, Willpower, Agility and Endurance add up to the fatigue
    /// maximum, and Intelligence makes the magicka maximum. Null for any other stat.
    /// </summary>
    public static Stat? FeedsInto(Stat stat) => stat switch
    {
        Stat.Strength or Stat.Willpower or Stat.Agility or Stat.Endurance => Stat.Fatigue,
        Stat.Intelligence => Stat.Magicka,
        _ => null,
    };

    /// <summary>
    /// The statuses for a character in these tiers, hunger first. Drains on a vital, from both meters together, stop
    /// where the character keeps 1 fatigue and, with a <paramref name="healthFloor"/>, that much health:
    /// <paramref name="health"/> is the character's health without the mod's own Drain Health. Attribute drains that
    /// lower the fatigue maximum count against that 1 as well. A Damage Health, which takes health every second, is
    /// left out while <paramref name="healthDamage"/> is false.
    /// </summary>
    public static List<WantedStatus> Wanted(
        Tier? hunger, Tier? thirst, NaturalVitals natural, float health, int healthFloor, bool healthDamage)
    {
        Tier[] tiers = [.. new[] { hunger, thirst }.OfType<Tier>()];
        int attributesOffFatigue = tiers
            .SelectMany(tier => tier.Effects)
            .Where(recipe => recipe is { Kind: StatusKind.Drain, Stat: { } stat } && FeedsInto(stat) == Stat.Fatigue)
            .Sum(recipe => recipe.Points);
        Dictionary<Stat, int> room = new()
        {
            [Stat.Health] = healthFloor > 0
                ? (int)Math.Max(0f, MathF.Min(natural.Health, health) - healthFloor)
                : (int)natural.Health,
            [Stat.Magicka] = (int)natural.Magicka,
            [Stat.Fatigue] = (int)Math.Max(0f, natural.Fatigue - 1f - attributesOffFatigue),
        };

        List<WantedStatus> wanted = [];
        foreach (Tier tier in tiers)
        {
            foreach (EffectRecipe recipe in tier.Effects)
            {
                if (recipe is { Kind: StatusKind.Damage, Vital: Stat.Health } && !healthDamage)
                    continue;

                int magnitude = recipe is { Percent: > 0, Vital: { } of }
                    ? (int)Math.Round(recipe.Percent / 100f * natural.Of(of))
                    : recipe.Points;
                if (recipe is { Kind: StatusKind.Drain, Vital: { } drained })
                {
                    magnitude = Math.Min(magnitude, room[drained]);
                    room[drained] -= Math.Max(0, magnitude);
                }
                if (magnitude <= 0)
                    continue;

                StatusEffect effect = recipe.Kind is { } kind
                    ? StatusEffect.Of(kind, recipe.Stat!.Value, magnitude)
                    : StatusEffect.Custom(recipe.Code!, magnitude);
                wanted.Add(new WantedStatus(effect.WithName(tier.Name), recipe.Vital));
            }
        }
        return wanted;
    }
}
