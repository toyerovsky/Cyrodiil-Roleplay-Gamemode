namespace Roleplay.Jobs.Server;

// One effect of an enchantment: Effect is the magic effect's four letter code (FIDG, FOAT, ...), Stat the stat it works
// on for effects that name one (Strength, Health, Blade, ...), Duration how many seconds a weapon's effect lasts on
// whoever it strikes, 0 for apparel's constant one.
public sealed class JobEnchantmentEffect
{
    public string Effect { get; set; } = "";
    public string Stat { get; set; } = "";
    public int Magnitude { get; set; }
    public int Duration { get; set; }
}
