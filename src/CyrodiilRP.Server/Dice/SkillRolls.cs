using CyrodiilRP.Server.Specializations;
using OblivionMP.SDK.Common.Statuses;

namespace CyrodiilRP.Server.Dice;

// A roll with a skill or an attribute: a d20 and a bonus from the character's level in it.
internal static class SkillRolls
{
    // A shorter start of a name matches too much ("a", "sp").
    public const int MinShortName = 3;

    private const int ApprenticeFrom = 25;
    private const int JourneymanFrom = 50;
    private const int ExpertFrom = 75;
    private const int MasterFrom = 100;

    private const int ApprenticeBonus = 1;
    private const int JourneymanBonus = 2;
    private const int ExpertBonus = 3;
    private const int MasterBonus = 5;

    // The stats a name means: the one it names in full, else every one it starts, so one match is found and more are
    // ambiguous. Spaces and letter case do not count: "heavy armor", "HeavyArmor" and "heavy" all name Heavy Armor.
    public static IReadOnlyList<Stat> Find(string name)
    {
        var key = string.Concat(name.Where(c => !char.IsWhiteSpace(c)));
        var stats = SpecializationRules.Attributes.Concat(SpecializationRules.Skills).ToList();
        var exact = stats.Where(stat => string.Equals(stat.ToString(), key, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0 || key.Length < MinShortName)
            return exact;

        return stats.Where(stat => stat.ToString().StartsWith(key, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // Novice +0, Apprentice +1, Journeyman +2, Expert +3, Master +5, as the game names the levels of a skill.
    public static int FindBonus(int value) => value switch
    {
        >= MasterFrom => MasterBonus,
        >= ExpertFrom => ExpertBonus,
        >= JourneymanFrom => JourneymanBonus,
        >= ApprenticeFrom => ApprenticeBonus,
        _ => 0,
    };

    public static int CountDice(SkillRollMode mode) => mode switch
    {
        SkillRollMode.Normal => 1,
        SkillRollMode.Advantage or SkillRollMode.Disadvantage => 2,
    };

    // The die that counts: the one rolled, the higher with an advantage, the lower with a disadvantage.
    public static int Keep(SkillRollMode mode, IReadOnlyList<RolledDie> dice) => mode switch
    {
        SkillRollMode.Normal => dice.Single().Value,
        SkillRollMode.Advantage => dice.Max(die => die.Value),
        SkillRollMode.Disadvantage => dice.Min(die => die.Value),
    };
}
