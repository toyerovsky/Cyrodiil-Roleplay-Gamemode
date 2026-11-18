using CyrodiilRP.Data;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Specializations;
using CyrodiilRP.Server.Traders;
using OblivionMP.SDK.Common.Skills;
using OblivionMP.SDK.Common.Statuses;

namespace CyrodiilRP.Tests.Specializations;

public sealed class SpecializationRulesTests
{
    private static readonly SkillCapMessages Texts = new("train", "book", "potion", "held");
    private static readonly SpecializationSettings Settings = new();

    private static readonly Stat[] Melee = [Stat.Blade, Stat.Blunt, Stat.HandToHand, Stat.Block, Stat.HeavyArmor, Stat.LightArmor];

    [Fact]
    public void There_are_nine_specializations_three_in_each_path()
    {
        Assert.Equal(Enum.GetValues<SpecializationKind>(), SpecializationRules.Profiles.Select(profile => profile.Kind));
        foreach (var path in Enum.GetValues<SpecializationPath>())
            Assert.Equal(3, SpecializationRules.Profiles.Count(profile => profile.Path == path));
    }

    [Fact]
    public void A_stat_is_mastered_or_trained_never_both_and_never_a_vital_or_the_universal_speed()
    {
        foreach (var profile in SpecializationRules.Profiles)
        {
            Assert.Empty(profile.Mastered.Intersect(profile.Trained));
            Assert.Equal(profile.Mastered.Count, profile.Mastered.Distinct().Count());
            Assert.Equal(profile.Trained.Count, profile.Trained.Distinct().Count());
            Assert.All(profile.Mastered.Concat(profile.Trained), stat =>
            {
                Assert.NotEqual(StatGroup.Vital, stat.Group());
                Assert.DoesNotContain(stat, SpecializationRules.Universal);
            });
        }
    }

    [Fact]
    public void A_wizard_masters_destruction_and_brews_only_to_60_while_an_alchemist_is_the_inverse()
    {
        var wizard = Caps(SpecializationKind.Wizard);
        var alchemist = Caps(SpecializationKind.Alchemist);

        Assert.Null(wizard.FindCap(Stat.Destruction));
        Assert.Equal(60, wizard.FindCap(Stat.Alchemy));
        Assert.Null(alchemist.FindCap(Stat.Alchemy));
        Assert.Equal(60, alchemist.FindCap(Stat.Destruction));
    }

    [Theory]
    [InlineData(SpecializationKind.Wizard)]
    [InlineData(SpecializationKind.Alchemist)]
    [InlineData(SpecializationKind.Illusionist)]
    public void No_mage_goes_past_30_in_melee_or_light_armor(SpecializationKind mage)
    {
        var caps = Caps(mage);

        Assert.All(Melee, stat => Assert.Equal(30, caps.FindCap(stat)));
    }

    [Fact]
    public void Every_skill_and_attribute_is_mastered_trained_or_limited_and_speed_never_capped()
    {
        foreach (var profile in SpecializationRules.Profiles)
        {
            var caps = Caps(profile.Kind);
            foreach (var stat in SpecializationRules.Attributes.Concat(SpecializationRules.Skills))
            {
                var attribute = stat.Group() == StatGroup.Attribute;
                int? expected = profile.Mastered.Contains(stat) || stat == Stat.Speed ? null
                    : profile.Trained.Contains(stat) ? attribute ? 75 : 60
                    : attribute ? 50 : 30;
                Assert.Equal(expected, caps.FindCap(stat));
            }

            Assert.Equal(29 - profile.Mastered.Count - 1, caps.Caps.Count);
        }
    }

    [Fact]
    public void A_specializations_class_has_seven_majors_every_mastered_skill_first_then_trained_ones()
    {
        foreach (var profile in SpecializationRules.Profiles)
        {
            var caps = Caps(profile.Kind);
            var masteredSkills = profile.Mastered.Where(stat => stat.Group() == StatGroup.Skill).ToList();

            Assert.Equal(7, caps.MajorSkills.Count);
            Assert.All(masteredSkills, skill => Assert.True(caps.IsMajor(skill), $"{profile.Kind} masters {skill}, which is not major"));
            Assert.All(caps.MajorSkills, skill => Assert.True(masteredSkills.Contains(skill) || profile.Trained.Contains(skill)));
            Assert.Equal(profile.Mastered.Concat(profile.Trained).Where(stat => stat.Group() == StatGroup.Skill).Take(7),
                SpecializationRules.FindMajorSkills(profile));
        }

        Assert.Equal([Stat.Destruction, Stat.Alteration, Stat.Mysticism, Stat.Conjuration, Stat.Illusion, Stat.Restoration, Stat.Alchemy],
            SpecializationRules.FindMajorSkills(SpecializationRules.FindProfile(SpecializationKind.Wizard)));
    }

    [Fact]
    public void Potions_fortify_only_what_a_specialization_masters_and_speed()
    {
        var knight = Caps(SpecializationKind.Knight);

        Assert.Equal(PotionRule.PlayerMade, knight.PotionRule);
        Assert.Equal(
            SpecializationRules.FindProfile(SpecializationKind.Knight).Mastered.Append(Stat.Speed).Order(),
            knight.PotionStats.Order());
        Assert.DoesNotContain(Stat.Intelligence, knight.PotionStats);
    }

    [Fact]
    public void Before_the_choice_every_skill_stops_at_the_unchosen_cap_and_nothing_else_is_held()
    {
        var caps = SpecializationRules.BuildCaps(null, Settings, Texts);

        Assert.Equal(21, caps.Caps.Count);
        Assert.All(SpecializationRules.Skills, skill => Assert.Equal(50, caps.FindCap(skill)));
        Assert.All(SpecializationRules.Attributes, attribute => Assert.Null(caps.FindCap(attribute)));
        Assert.Equal(PotionRule.Off, caps.PotionRule);
        Assert.Empty(caps.MajorSkills);
        Assert.Same(Texts, caps.Messages);
    }

    [Fact]
    public void The_caps_follow_the_config()
    {
        var settings = new SpecializationSettings { TrainedSkillCap = 70, LimitedSkillCap = 10, LimitedAttributeCap = 40, Potions = PotionRule.All };

        var caps = SpecializationRules.BuildCaps(SpecializationKind.Wizard, settings, Texts);

        Assert.Equal(70, caps.FindCap(Stat.Alchemy));
        Assert.Equal(10, caps.FindCap(Stat.Blade));
        Assert.Equal(40, caps.FindCap(Stat.Strength));
        Assert.Equal(PotionRule.All, caps.PotionRule);
    }

    [Fact]
    public void A_mentor_teaches_its_path_and_the_registrar_every_specialization()
    {
        Assert.Equal([SpecializationKind.Knight, SpecializationKind.Barbarian, SpecializationKind.Ranger],
            SpecializationRules.FindOffered(TraderSpotKind.WarriorMentor));
        Assert.Equal([SpecializationKind.Wizard, SpecializationKind.Alchemist, SpecializationKind.Illusionist],
            SpecializationRules.FindOffered(TraderSpotKind.MageMentor));
        Assert.Equal([SpecializationKind.Healer, SpecializationKind.Smith, SpecializationKind.Thief],
            SpecializationRules.FindOffered(TraderSpotKind.SupportMentor));
        Assert.Equal(Enum.GetValues<SpecializationKind>(), SpecializationRules.FindOffered(TraderSpotKind.Registrar));
        Assert.Null(SpecializationRules.FindPath(TraderSpotKind.Registrar));
        Assert.Equal(SpecializationPath.Mage, SpecializationRules.FindPath(TraderSpotKind.MageMentor));
    }

    [Fact]
    public void Traders_bankers_and_stablemasters_teach_nothing()
    {
        foreach (var kind in new[] { TraderSpotKind.Shop, TraderSpotKind.Banker, TraderSpotKind.Stablemaster })
        {
            Assert.Empty(SpecializationRules.FindOffered(kind));
            Assert.False(SpecializationRules.IsTeacher(kind));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpecializationRules.FindPath(kind));
        }
    }

    [Fact]
    public void Each_teacher_is_stored_under_a_shop_of_its_own_and_marked_by_an_origin_of_its_own()
    {
        var teachers = new[] { TraderSpotKind.WarriorMentor, TraderSpotKind.MageMentor, TraderSpotKind.SupportMentor, TraderSpotKind.Registrar };
        var shops = new[] { TraderRules.WarriorMentorShop, TraderRules.MageMentorShop, TraderRules.SupportMentorShop, TraderRules.RegistrarShop };

        Assert.Equal(teachers, shops.Select(TraderRules.FindKind));
        var origins = Enum.GetValues<TraderSpotKind>().Select(TraderRules.FindOrigin).ToList();
        Assert.Equal(origins.Count, origins.Distinct().Count());
        Assert.All(origins, origin => Assert.True(TraderRules.IsOurs(origin)));
    }

    [Theory]
    [InlineData(-5.0, "specialization.wait.seconds", 1)]
    [InlineData(0.0, "specialization.wait.seconds", 1)]
    [InlineData(0.2, "specialization.wait.seconds", 1)]
    [InlineData(29.4, "specialization.wait.seconds", 30)]
    [InlineData(59.9, "specialization.wait.seconds", 60)]
    [InlineData(60.0, "specialization.wait.minutes", 1)]
    [InlineData(90.0, "specialization.wait.minutes", 2)]
    [InlineData(3599.0, "specialization.wait.minutes", 60)]
    [InlineData(3600.0, "specialization.wait.hours", 1)]
    [InlineData(255600.5, "specialization.wait.hours", 72)]
    public void A_wait_is_said_in_its_largest_whole_unit_rounded_up(double seconds, string key, int count)
    {
        var now = DateTimeOffset.UnixEpoch;

        var wait = SpecializationRules.DescribeWait(now, now.AddSeconds(seconds));

        Assert.Equal(key, wait.Key);
        Assert.Equal(new TextArg("count", count), Assert.Single(wait.Args));
    }

    [Fact]
    public void A_change_waits_the_cooldown_and_costs_the_change_price_while_the_first_choice_does_not()
    {
        var chosenAt = DateTimeOffset.UnixEpoch;

        Assert.Equal(chosenAt.AddSeconds(SpecializationSettings.DefaultCooldownSeconds), SpecializationRules.FindNextChoice(chosenAt, Settings));
        Assert.Equal(chosenAt.AddHours(72), SpecializationRules.FindNextChoice(chosenAt, new SpecializationSettings { ChangeCooldownSeconds = 259200 }));
        Assert.Equal(0, SpecializationRules.FindPrice(first: true, Settings));
        Assert.Equal(1000, SpecializationRules.FindPrice(first: false, Settings));
    }

    [Fact]
    public void A_page_sends_a_specialization_by_its_name_and_nothing_else_reads()
    {
        foreach (var kind in Enum.GetValues<SpecializationKind>())
        {
            Assert.True(SpecializationRules.TryRead(kind.ToString(), out var read));
            Assert.Equal(kind, read);
        }

        Assert.False(SpecializationRules.TryRead("knight", out _));
        Assert.False(SpecializationRules.TryRead("Paladin", out _));
    }

    [Fact]
    public void A_change_names_the_new_specialization_the_old_one_and_the_price_each_in_its_place()
    {
        var change = SpecializationRules.DescribeChange(SpecializationKind.Healer, SpecializationKind.Wizard, 1000);

        Assert.Equal("specialization.changed", change.Key);
        Assert.Equal(SpecializationRules.FindName(SpecializationKind.Healer), change.Args.Single(arg => arg.Name == "specialization").Value);
        Assert.Equal(SpecializationRules.FindName(SpecializationKind.Wizard), change.Args.Single(arg => arg.Name == "old").Value);
        Assert.Equal(1000, change.Args.Single(arg => arg.Name == "price").Value);
    }

    [Fact]
    public void A_staff_cap_replaces_the_specializations_own_or_caps_a_stat_it_left_free()
    {
        var overrides = new Dictionary<Stat, int> { [Stat.Blade] = 80, [Stat.Destruction] = 40, [Stat.Speed] = 70 };

        var caps = SpecializationRules.BuildCaps(SpecializationKind.Wizard, Settings, Texts, overrides);

        Assert.Equal(80, caps.FindCap(Stat.Blade));
        Assert.Equal(40, caps.FindCap(Stat.Destruction));
        Assert.Equal(70, caps.FindCap(Stat.Speed));
        Assert.Equal(60, caps.FindCap(Stat.Alchemy));
        Assert.Equal(Caps(SpecializationKind.Wizard).MajorSkills.Order(), caps.MajorSkills.Order());
    }

    [Fact]
    public void A_staff_cap_holds_before_the_first_choice_too()
    {
        var caps = SpecializationRules.BuildCaps(null, Settings, Texts, new Dictionary<Stat, int> { [Stat.Sneak] = 10, [Stat.Luck] = 20 });

        Assert.Equal(10, caps.FindCap(Stat.Sneak));
        Assert.Equal(20, caps.FindCap(Stat.Luck));
        Assert.Equal(50, caps.FindCap(Stat.Blade));
    }

    [Theory]
    [InlineData("Blade", Stat.Blade)]
    [InlineData("handtohand", Stat.HandToHand)]
    [InlineData("STRENGTH", Stat.Strength)]
    public void A_staff_command_names_an_attribute_or_a_skill_in_any_case(string word, Stat expected)
    {
        Assert.True(SpecializationRules.TryReadStat(word, out var stat));
        Assert.Equal(expected, stat);
    }

    [Theory]
    [InlineData("Health")]
    [InlineData("Hand to Hand")]
    [InlineData("")]
    [InlineData("12")]
    public void A_vital_or_anything_else_is_no_stat_a_staff_command_takes(string word)
    {
        Assert.False(SpecializationRules.TryReadStat(word, out _));
    }

    [Fact]
    public void A_staff_command_names_a_specialization_in_any_case()
    {
        Assert.True(SpecializationRules.TryReadAnyCase("wizard", out var kind));
        Assert.Equal(SpecializationKind.Wizard, kind);
        Assert.False(SpecializationRules.TryReadAnyCase("none", out _));
        Assert.False(SpecializationRules.TryRead("wizard", out _));
    }

    [Fact]
    public void Every_specialization_has_a_name_of_its_own()
    {
        var kinds = Enum.GetValues<SpecializationKind>();

        Assert.Equal(kinds.Length, kinds.Select(kind => SpecializationRules.FindName(kind).Key).Distinct().Count());
    }

    private static SkillCaps Caps(SpecializationKind kind) => SpecializationRules.BuildCaps(kind, Settings, Texts);
}
