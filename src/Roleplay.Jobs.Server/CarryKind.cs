namespace Roleplay.Jobs.Server;

/// <summary>Something that can be carried: its model, and its category, whose weight and fit it takes unless it gives
/// its own.</summary>
public sealed class CarryKind
{
    public string Name { get; set; } = "";

    // A TES form's Unreal asset, as a prop takes it (/Game/Forms/<group>/<type>/<EditorID>.<EditorID>), or a StaticMesh
    public string Asset { get; set; } = "";

    // The model's size, in the world and in the hand
    public float Scale { get; set; } = 1f;

    // The name of its CarryCategory
    public string Category { get; set; } = "";

    // Its own, over the category's: null takes the category's
    public int? Weight { get; set; }
    public int? DrainSpeed { get; set; }
    public CarryFit? Standing { get; set; }
    public CarryFit? Moving { get; set; }
}
