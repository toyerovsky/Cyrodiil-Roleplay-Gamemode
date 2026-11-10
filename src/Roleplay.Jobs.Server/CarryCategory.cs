namespace Roleplay.Jobs.Server;

/// <summary>A shape of thing to carry, such as a beam or a box: how heavy one is and how it hangs in the hand, for every
/// kind of that category that does not say otherwise.</summary>
public sealed class CarryCategory
{
    public string Name { get; set; } = "";

    // A Burden on the carrier, in the game's weight units, and points of Speed drained, while they carry one: 0 for none
    public int Weight { get; set; }
    public int DrainSpeed { get; set; }

    // How one hangs while the carrier stands, and while they move: null moving keeps the standing fit
    public CarryFit Standing { get; set; } = new();
    public CarryFit? Moving { get; set; }
}
