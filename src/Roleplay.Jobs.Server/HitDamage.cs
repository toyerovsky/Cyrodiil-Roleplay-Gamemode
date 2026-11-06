using OblivionMP.SDK.Common.Archetypes;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>The arithmetic of <see cref="IHitRules"/>. Pure, so the server and the tests agree.</summary>
public static class HitDamage
{
    /// <summary>The damage one hit does: the attack's own, the bonus of the character's Strength, and the bonus of the
    /// tool's tiers above the material's (a steel axe on pine: one).</summary>
    /// <param name="tiersAbove">How many tiers the tool is above the tree's wood or the vein's ore; 0 when not above, or
    /// when no tool is needed.</param>
    public static int DamageOf(JobAttack attack, int strength, int tiersAbove, IHitRules rules)
    {
        var above = Math.Max(0, tiersAbove);
        return attack switch
        {
            JobAttack.Normal => rules.NormalDamage
                                + StrengthBonus(strength, rules.NormalStrengthFrom, rules.NormalStrengthStep, rules.NormalStrengthBonus)
                                + above * rules.NormalTierBonus,
            JobAttack.Power => rules.PowerDamage
                               + StrengthBonus(strength, rules.PowerStrengthFrom, rules.PowerStrengthStep, rules.PowerStrengthBonus)
                               + above * rules.PowerTierBonus,
            _ => throw new ArgumentOutOfRangeException(nameof(attack), attack, "Not a job attack."),
        };
    }

    /// <summary>How many tiers the tool is above the material; 0 when it is not, or when no tool is needed.</summary>
    public static int TiersAbove(int? toolTier, int materialTier)
        => toolTier is { } tier ? Math.Max(0, tier - materialTier) : 0;

    /// <summary>The share of its full durability a tool has left, 0 to 1: 1 for one as made (the game's -1) or of a
    /// durability the config does not know.</summary>
    public static float ConditionOf(float health, int durability)
        => health < 0f || durability <= 0 ? 1f : Math.Clamp(health / durability, 0f, 1f);

    /// <summary>The chance a hit with a tool of this condition glances off, doing nothing: none from DullBelow up, then
    /// rising evenly to MaxGlanceChance as the tool nears broken. A broken tool (condition 0) never bites.</summary>
    public static float GlanceChance(float condition, IHitRules rules)
    {
        if (condition <= 0f)
            return 1f;
        if (rules.DullBelow <= 0f || condition >= rules.DullBelow)
            return 0f;
        return Math.Clamp(rules.MaxGlanceChance, 0f, 1f) * (1f - condition / rules.DullBelow);
    }

    /// <summary>How soon after a player's last hit this one may come.</summary>
    public static float MinSecondsBefore(JobAttack attack, IHitRules rules) => attack switch
    {
        JobAttack.Normal => rules.MinSecondsBetweenHits,
        JobAttack.Power => rules.MinSecondsBetweenPowerHits,
        _ => throw new ArgumentOutOfRangeException(nameof(attack), attack, "Not a job attack."),
    };

    /// <summary>The character's Strength as it is now, fortified or drained; 0 before its attributes came in.</summary>
    public static int StrengthOf(MainCharacter character)
        => character.ContainsSkillAttributeKey((byte)CharacterAttribute.Strength)
            ? (int)character.GetSkillAttribute((byte)CharacterAttribute.Strength)
            : 0;

    // bonus for every whole step of Strength above from; a step of 0 is no bonus
    private static int StrengthBonus(int strength, int from, int step, int bonus)
        => step <= 0 || strength <= from ? 0 : (strength - from) / step * bonus;
}
