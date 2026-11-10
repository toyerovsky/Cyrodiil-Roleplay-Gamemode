namespace Roleplay.Jobs.Server;

// A custom item's enchantment: a weapon's holds Charge and spends it on each strike, apparel's is constant.
public sealed class JobEnchantment
{
    public int Charge { get; set; }
    public List<JobEnchantmentEffect> Effects { get; set; } = [];
}
