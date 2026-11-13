namespace Roleplay.Jobs.Server;

/// <summary>What <see cref="DocksStore"/> writes: the crate deposits and the foremen.</summary>
public sealed class DocksSave
{
    public List<DockZone> Zones { get; set; } = [];
    public List<Foreman> Foremen { get; set; } = [];

    // The crate's size along its own axes, as a client measured it; 0 before one has
    public float CrateX { get; set; }
    public float CrateY { get; set; }
    public float CrateZ { get; set; }
    // How far the crate's bottom is above its origin
    public float CrateBottom { get; set; }

    // The pallet board's box at scale 1 around its origin, as a client measured it; all 0 before one has
    public float BoardMinX { get; set; }
    public float BoardMinY { get; set; }
    public float BoardMinZ { get; set; }
    public float BoardMaxX { get; set; }
    public float BoardMaxY { get; set; }
    public float BoardMaxZ { get; set; }
}
