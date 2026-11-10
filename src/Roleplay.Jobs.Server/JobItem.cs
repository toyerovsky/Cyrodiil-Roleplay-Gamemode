namespace Roleplay.Jobs.Server;

// An item the mod makes, as a copy of Base (a form id) with whatever is set here in place of the base's: Name, Value,
// Weight, Damage (a weapon's), Armor (an armor's rating, as the game shows it), Mesh, the Unreal asset it is shown with
// (a StaticMesh, or a TES form's asset whose mesh is taken), Scale, how big it is shown on the ground and in hand, Icon,
// the form id of the item whose inventory icon it shows, and Enchantment. A drop or an axe names it as "custom:" and
// its Key.
public sealed class JobItem
{
    public string Key { get; set; } = "";
    public string Base { get; set; } = "";
    public string Name { get; set; } = "";
    public int? Value { get; set; }
    public float? Weight { get; set; }
    public int? Damage { get; set; }
    public float? Armor { get; set; }
    public string Mesh { get; set; } = "";
    public float? Scale { get; set; }
    public string Icon { get; set; } = "";
    public JobEnchantment? Enchantment { get; set; }
}
