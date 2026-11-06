namespace Roleplay.HungerAndThirst.Server;

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
