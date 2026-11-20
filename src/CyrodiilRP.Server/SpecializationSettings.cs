using System.Text.Json.Serialization;
using OblivionMP.SDK.Common.Skills;

namespace CyrodiilRP.Server;

// What a specialization costs and how high it lets a character grow. A specialization's own stats grow to 100; its
// trained stats stop at the trained caps and the rest at the limited ones. A character who has not chosen one yet stops
// at UnchosenSkillCap in every skill.
internal sealed class SpecializationSettings
{
    // Gold for the first choice. A change after it costs ChangePrice.
    public int FirstPrice { get; set; }

    public int ChangePrice { get; set; } = 1000;

    // Real seconds between two choices. 0 lets a character change at once. Without a value in the config, a Debug build
    // waits 30 s and a Release build a minute, so a test can change it twice in a row.
    public int ChangeCooldownSeconds { get; set; } = DefaultCooldownSeconds;

    public int TrainedSkillCap { get; set; } = 60;

    public int LimitedSkillCap { get; set; } = 30;

    public int TrainedAttributeCap { get; set; } = 75;

    public int LimitedAttributeCap { get; set; } = 50;

    public int UnchosenSkillCap { get; set; } = 50;

    // Which potions follow a specialization: those the players made, every one, or none. By name in the config: the
    // server reads an enum as a number unless its property names a converter.
    [JsonConverter(typeof(JsonStringEnumConverter<PotionRule>))]
    public PotionRule Potions { get; set; } = PotionRule.PlayerMade;

    public TimeSpan ChangeCooldown => TimeSpan.FromSeconds(ChangeCooldownSeconds);

#if DEBUG
    public const int DefaultCooldownSeconds = 30;
#else
    public const int DefaultCooldownSeconds = 60;
#endif

    // A wrong value stops the mod at start.
    public void RequireValid()
    {
        RequireAtLeastZero(nameof(FirstPrice), FirstPrice);
        RequireAtLeastZero(nameof(ChangePrice), ChangePrice);
        RequireAtLeastZero(nameof(ChangeCooldownSeconds), ChangeCooldownSeconds);
        RequireCap(nameof(TrainedSkillCap), TrainedSkillCap);
        RequireCap(nameof(LimitedSkillCap), LimitedSkillCap);
        RequireCap(nameof(TrainedAttributeCap), TrainedAttributeCap);
        RequireCap(nameof(LimitedAttributeCap), LimitedAttributeCap);
        RequireCap(nameof(UnchosenSkillCap), UnchosenSkillCap);
        if (!Enum.IsDefined(Potions))
            throw new InvalidOperationException($"server/config.json: specializations.potions is {Potions}, which is no potion rule.");
    }

    private static void RequireAtLeastZero(string name, int value)
    {
        if (value < 0)
            throw new InvalidOperationException($"server/config.json: specializations.{name} is {value}, below 0.");
    }

    private static void RequireCap(string name, int value)
    {
        if (value is < 0 or > SkillCaps.MostCap)
            throw new InvalidOperationException($"server/config.json: specializations.{name} is {value}, not 0 to {SkillCaps.MostCap}.");
    }
}
