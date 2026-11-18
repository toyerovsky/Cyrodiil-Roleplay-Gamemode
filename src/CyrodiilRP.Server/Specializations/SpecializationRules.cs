using CyrodiilRP.Data;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Traders;
using OblivionMP.SDK.Common.Skills;
using OblivionMP.SDK.Common.Statuses;

namespace CyrodiilRP.Server.Specializations;

internal static class SpecializationRules
{
    // Mastered by every specialization, and fortified by anyone's potions.
    public static readonly IReadOnlyList<Stat> Universal = [Stat.Speed];

    public static readonly IReadOnlyList<Stat> Attributes = [.. Enum.GetValues<Stat>().Where(stat => stat.Group() == StatGroup.Attribute)];

    public static readonly IReadOnlyList<Stat> Skills = [.. Enum.GetValues<Stat>().Where(stat => stat.Group() == StatGroup.Skill)];

    public static readonly IReadOnlyList<SpecializationProfile> Profiles =
    [
        new(SpecializationKind.Knight, SpecializationPath.Warrior,
            Mastered: [Stat.Strength, Stat.Endurance, Stat.Blade, Stat.Blunt, Stat.Block, Stat.HeavyArmor, Stat.Armorer],
            Trained: [Stat.Willpower, Stat.Personality, Stat.HandToHand, Stat.Athletics, Stat.Speechcraft, Stat.Restoration]),
        new(SpecializationKind.Barbarian, SpecializationPath.Warrior,
            Mastered: [Stat.Strength, Stat.Endurance, Stat.Blade, Stat.Blunt, Stat.HandToHand, Stat.Athletics, Stat.Acrobatics],
            Trained: [Stat.Agility, Stat.Luck, Stat.LightArmor, Stat.Block, Stat.Armorer, Stat.Marksman]),
        new(SpecializationKind.Ranger, SpecializationPath.Warrior,
            Mastered: [Stat.Agility, Stat.Endurance, Stat.Marksman, Stat.LightArmor, Stat.Sneak, Stat.Athletics, Stat.Acrobatics],
            Trained: [Stat.Strength, Stat.Willpower, Stat.Blade, Stat.Alchemy, Stat.Security, Stat.Restoration]),
        new(SpecializationKind.Wizard, SpecializationPath.Mage,
            Mastered: [Stat.Intelligence, Stat.Willpower, Stat.Destruction, Stat.Alteration, Stat.Mysticism],
            Trained: [Stat.Personality, Stat.Luck, Stat.Conjuration, Stat.Illusion, Stat.Restoration, Stat.Alchemy, Stat.Speechcraft]),
        new(SpecializationKind.Alchemist, SpecializationPath.Mage,
            Mastered: [Stat.Intelligence, Stat.Willpower, Stat.Alchemy, Stat.Restoration, Stat.Mysticism],
            Trained: [Stat.Personality, Stat.Luck, Stat.Alteration, Stat.Illusion, Stat.Destruction, Stat.Conjuration, Stat.Mercantile]),
        new(SpecializationKind.Illusionist, SpecializationPath.Mage,
            Mastered: [Stat.Intelligence, Stat.Personality, Stat.Illusion, Stat.Conjuration, Stat.Speechcraft],
            Trained: [Stat.Willpower, Stat.Agility, Stat.Mysticism, Stat.Alteration, Stat.Restoration, Stat.Alchemy, Stat.Sneak]),
        new(SpecializationKind.Healer, SpecializationPath.Support,
            Mastered: [Stat.Willpower, Stat.Personality, Stat.Restoration, Stat.Alchemy, Stat.Speechcraft],
            Trained: [Stat.Intelligence, Stat.Endurance, Stat.Mysticism, Stat.Alteration, Stat.Illusion, Stat.Mercantile, Stat.Athletics]),
        new(SpecializationKind.Smith, SpecializationPath.Support,
            Mastered: [Stat.Strength, Stat.Endurance, Stat.Armorer, Stat.Mercantile, Stat.Blunt, Stat.HeavyArmor],
            Trained: [Stat.Personality, Stat.Luck, Stat.Speechcraft, Stat.Block, Stat.Athletics, Stat.Blade, Stat.Security]),
        new(SpecializationKind.Thief, SpecializationPath.Support,
            Mastered: [Stat.Agility, Stat.Luck, Stat.Sneak, Stat.Security, Stat.Acrobatics, Stat.LightArmor, Stat.Mercantile],
            Trained: [Stat.Personality, Stat.Endurance, Stat.Blade, Stat.Marksman, Stat.Speechcraft, Stat.Illusion, Stat.Athletics]),
    ];

    public static SpecializationProfile FindProfile(SpecializationKind kind) => Profiles.Single(profile => profile.Kind == kind);

    // The specializations a mode's person teaches: a mentor those of its path, the registrar all of them.
    public static IReadOnlyList<SpecializationKind> FindOffered(TraderSpotKind spot) => spot switch
    {
        TraderSpotKind.WarriorMentor => OfPath(SpecializationPath.Warrior),
        TraderSpotKind.MageMentor => OfPath(SpecializationPath.Mage),
        TraderSpotKind.SupportMentor => OfPath(SpecializationPath.Support),
        TraderSpotKind.Registrar => [.. Profiles.Select(profile => profile.Kind)],
        TraderSpotKind.Shop or TraderSpotKind.Banker or TraderSpotKind.Stablemaster => [],
    };

    public static bool IsTeacher(TraderSpotKind spot) => FindOffered(spot).Count > 0;

    // A mentor's path, null for the registrar, who teaches every path.
    public static SpecializationPath? FindPath(TraderSpotKind spot) => spot switch
    {
        TraderSpotKind.WarriorMentor => SpecializationPath.Warrior,
        TraderSpotKind.MageMentor => SpecializationPath.Mage,
        TraderSpotKind.SupportMentor => SpecializationPath.Support,
        TraderSpotKind.Registrar => null,
        TraderSpotKind.Shop or TraderSpotKind.Banker or TraderSpotKind.Stablemaster =>
            throw new ArgumentOutOfRangeException(nameof(spot), spot, "This person teaches no specialization."),
    };

    // The wait in its largest whole unit, rounded up, so a wait under a minute still says 1 s.
    public static Phrase DescribeWait(DateTimeOffset now, DateTimeOffset next)
    {
        var left = next - now;
        if (left >= OneHour)
            return SpecializationText.WaitHours((int)Math.Ceiling(left.TotalHours));
        if (left >= OneMinute)
            return SpecializationText.WaitMinutes((int)Math.Ceiling(left.TotalMinutes));

        return SpecializationText.WaitSeconds(Math.Max(1, (int)Math.Ceiling(left.TotalSeconds)));
    }

    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    // The caps of a chosen specialization, or of a character who has not chosen one when kind is null, who keeps the
    // major skills of the class they made.
    public static SkillCaps BuildCaps(SpecializationKind? kind, SpecializationSettings settings, SkillCapMessages messages) =>
        BuildCaps(kind, settings, messages, NoOverrides);

    // As above, with the caps the staff set in place of the specialization's.
    public static SkillCaps BuildCaps(SpecializationKind? kind, SpecializationSettings settings, SkillCapMessages messages,
        IReadOnlyDictionary<Stat, int> overrides)
    {
        var caps = BuildSpecializationCaps(kind, settings, messages);
        foreach (var (stat, cap) in overrides)
            caps = caps.WithCap(stat, cap);

        return caps;
    }

    public static readonly IReadOnlyDictionary<Stat, int> NoOverrides = new Dictionary<Stat, int>();

    // A stat a staff command names: an attribute or a skill by its name, in any case. False for anything else.
    public static bool TryReadStat(string name, out Stat stat)
    {
        foreach (var each in Attributes.Concat(Skills))
        {
            if (string.Equals(each.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                stat = each;
                return true;
            }
        }

        stat = default;
        return false;
    }

    // A specialization a staff command names, in any case. False for anything else.
    public static bool TryReadAnyCase(string name, out SpecializationKind kind)
    {
        foreach (var each in Enum.GetValues<SpecializationKind>())
        {
            if (string.Equals(each.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                kind = each;
                return true;
            }
        }

        kind = default;
        return false;
    }

    private static SkillCaps BuildSpecializationCaps(SpecializationKind? kind, SpecializationSettings settings, SkillCapMessages messages)
    {
        var caps = SkillCaps.None.WithMessages(messages);
        if (kind is not { } chosen)
        {
            foreach (var skill in Skills)
                caps = caps.WithCap(skill, settings.UnchosenSkillCap);

            return caps;
        }

        var profile = FindProfile(chosen);
        foreach (var stat in Attributes.Concat(Skills))
        {
            if (profile.Mastered.Contains(stat) || Universal.Contains(stat))
                continue;

            var trained = profile.Trained.Contains(stat);
            var cap = stat.Group() == StatGroup.Attribute
                ? trained ? settings.TrainedAttributeCap : settings.LimitedAttributeCap
                : trained ? settings.TrainedSkillCap : settings.LimitedSkillCap;
            caps = caps.WithCap(stat, cap);
        }

        return caps.WithPotions(settings.Potions, profile.Mastered.Concat(Universal)).WithMajors(FindMajorSkills(profile));
    }

    // The class's major skills: every skill the specialization masters, then trained ones in order, up to the game's 7.
    public static IReadOnlyList<Stat> FindMajorSkills(SpecializationProfile profile) =>
        [.. profile.Mastered.Concat(profile.Trained).Where(stat => stat.Group() == StatGroup.Skill).Take(SkillCaps.MostMajorSkills)];

    // What a written change says. Named, as the text's placeholders come in another order than the arguments read best.
    public static Phrase DescribeChange(SpecializationKind chosen, SpecializationKind before, int price) =>
        SpecializationText.Changed(specialization: FindName(chosen), old: FindName(before), price: price);

    // When a character who chose at chosenAt may choose again.
    public static DateTimeOffset FindNextChoice(DateTimeOffset chosenAt, SpecializationSettings settings) => chosenAt + settings.ChangeCooldown;

    public static int FindPrice(bool first, SpecializationSettings settings) => first ? settings.FirstPrice : settings.ChangePrice;

    public static Phrase FindName(SpecializationKind kind) => kind switch
    {
        SpecializationKind.Knight => SpecializationText.NameKnight,
        SpecializationKind.Barbarian => SpecializationText.NameBarbarian,
        SpecializationKind.Ranger => SpecializationText.NameRanger,
        SpecializationKind.Wizard => SpecializationText.NameWizard,
        SpecializationKind.Alchemist => SpecializationText.NameAlchemist,
        SpecializationKind.Illusionist => SpecializationText.NameIllusionist,
        SpecializationKind.Healer => SpecializationText.NameHealer,
        SpecializationKind.Smith => SpecializationText.NameSmith,
        SpecializationKind.Thief => SpecializationText.NameThief,
    };

    // The name a page sends, back to the specialization. False for any other.
    public static bool TryRead(string name, out SpecializationKind kind)
    {
        foreach (var each in Enum.GetValues<SpecializationKind>())
        {
            if (each.ToString() == name)
            {
                kind = each;
                return true;
            }
        }

        kind = default;
        return false;
    }

    private static IReadOnlyList<SpecializationKind> OfPath(SpecializationPath path) =>
        [.. Profiles.Where(profile => profile.Path == path).Select(profile => profile.Kind)];
}
