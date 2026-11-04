using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Server;

// server/config.json. Every key is optional; a missing one keeps the default below. Meters run from 0 to 1000.
[ModConfig]
public sealed partial class SustenanceConfig
{
    // Real minutes from full to empty while standing around.
    public float MinutesToStarve { get; set; } = 180f; //180f;
    public float MinutesToDehydrate { get; set; } = 120f; //120f;

    // Faster decay while doing these. When several apply, the largest wins.
    public float RunningMultiplier { get; set; } = 1.5f;
    public float SwimmingMultiplier { get; set; } = 1.5f;
    public float CombatMultiplier { get; set; } = 2f;

    // A character the server has never seen starts here.
    public int StartSatiety { get; set; } = 1000;
    public int StartHydration { get; set; } = 1000;

    // After death the meters restart here.
    public int RespawnSatiety { get; set; } = 500;
    public int RespawnHydration { get; set; } = 500;

    // What a low meter does to the character, as statuses the game shows with the active effects. Only the deepest
    // tier a meter is in counts, so each one lists all of its effects. Hunger's and thirst's add up.
    // Hunger is low blood sugar first (tired, short-tempered), then the body eating its own muscle, then organs giving
    // out. Strength also sets carry weight, so a starving character carries less without a Burden.
    public List<SustenanceTier> Hunger { get; set; } =
    [
        Step("Hungry", 50, Percent("Drain Fatigue", 20), Points("Drain Willpower", 5)),
        Step("Starving", 25, Percent("Drain Fatigue", 40), Points("Drain Strength", 10), Points("Drain Endurance", 10),
            Points("Drain Willpower", 10)),
        Step("Wasting Away", 0, Percent("Drain Fatigue", 60), Points("Drain Strength", 25),
            Points("Drain Endurance", 25), Points("Drain Willpower", 15), Percent("Drain Health", 25)),
    ];

    // Thirst goes to the head first (headache, poor focus), then dizziness and clumsiness, then a body that can barely
    // walk and starts to fail. Slow, not stopped: a dehydrated character can still crawl to water.
    public List<SustenanceTier> Thirst { get; set; } =
    [
        Step("Thirsty", 50, Percent("Drain Fatigue", 20), Points("Drain Intelligence", 5)),
        Step("Parched", 25, Percent("Drain Fatigue", 50), Points("Drain Intelligence", 15), Points("Drain Agility", 10),
            Points("Drain Willpower", 10)),
        Step("Dehydrated", 0, Percent("Drain Fatigue", 80), Points("Drain Intelligence", 25),
            Points("Drain Agility", 20), Points("Drain Speed", 30), Points("Damage Health", 1)),
    ];

    private static SustenanceTier Step(string name, float belowPercent, params TierEffect[] effects)
        => new() { Name = name, BelowPercent = belowPercent, Effects = [.. effects] };

    private static TierEffect Points(string effect, int points) => new() { Effect = effect, Points = points };

    private static TierEffect Percent(string effect, float percent) => new() { Effect = effect, Percent = percent };

    // The effects never take health below this: a Drain Health stops short of it, and a Damage Health comes off
    // when the character gets there. 0 lets hunger and thirst kill. Keep it above a few seconds of Damage Health,
    // since the server sees health a moment late.
    public int HealthFloor { get; set; } = 10;

    // Minimum time between two meals or sips.
    public float ConsumeCooldownSeconds { get; set; } = 1.5f;

    // What one sip from a lake or river gives (while swimming).
    public int WaterHydration { get; set; } = 150;

    // How often the meters of everyone online are written to SaveFile, in the server mod folder.
    public float SaveIntervalMinutes { get; set; } = 5f;
    public string SaveFile { get; set; } = "sustenance.json";

    // False starts from an empty table, so only Foods below count.
    public bool UseDefaultFoods { get; set; } = true;

    // Changes to the table: a new item, new values for a default one, or "remove": true to drop a default.
    public List<FoodEntry> Foods { get; set; } = [];
}

// One tier of SustenanceConfig.Hunger or Thirst: while the meter is below BelowPercent of full (0: once it is empty),
// the character has every effect listed, shown under Name.
public sealed class SustenanceTier
{
    public string Name { get; set; } = "";
    public float BelowPercent { get; set; }
    public List<TierEffect> Effects { get; set; } = [];
}

// One effect of a tier. Effect is "<Kind> <Stat>" (Drain Fatigue, Damage Health, Drain Strength: kinds are Fortify,
// Drain, Damage and Restore), Burden, Feather, or a magic effect's four letter code. Points is the magnitude, per
// second for Damage and Restore. Percent takes a share of the character's own maximum instead, for Health, Magicka
// and Fatigue.
public sealed class TierEffect
{
    public string Effect { get; set; } = "";
    public int Points { get; set; }
    public float Percent { get; set; }
}

// One line of SustenanceConfig.Foods. Item is the editor id from the SDK catalogue ("Apple", "DrinkAle") or a form
// id in hex ("0x00033680"). Name is what the satchel shows; it defaults to the item's default name or its editor id.
public sealed class FoodEntry
{
    public string Item { get; set; } = "";
    public string? Name { get; set; }
    public int Satiety { get; set; }
    public int Hydration { get; set; }
    public bool Remove { get; set; }
}
