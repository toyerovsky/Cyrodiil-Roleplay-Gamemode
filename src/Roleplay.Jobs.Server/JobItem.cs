namespace Roleplay.Jobs.Server;

// An item the mod makes, as a copy of Base (a form id) with whatever is set here in place of the base's: Name, Value,
// Weight, Damage (a weapon's), Mesh, the Unreal asset it is shown with (a StaticMesh, or a TES form's asset whose mesh
// is taken), and Scale, how big it is shown on the ground and in hand. A drop or an axe names it as "custom:" and its Key.
public sealed class JobItem
{
    public string Key { get; set; } = "";
    public string Base { get; set; } = "";
    public string Name { get; set; } = "";
    public int? Value { get; set; }
    public float? Weight { get; set; }
    public int? Damage { get; set; }
    public string Mesh { get; set; } = "";
    public float? Scale { get; set; }
}
