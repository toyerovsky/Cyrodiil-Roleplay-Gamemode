namespace Roleplay.HungerAndThirst.Server;

// One tier of SustenanceConfig.Hunger or Thirst: while the meter is below BelowPercent of full (0: once it is empty),
// the character has every effect listed, shown under Name.
public sealed class SustenanceTier
{
    public string Name { get; set; } = "";
    public float BelowPercent { get; set; }
    public List<TierEffect> Effects { get; set; } = [];
}
