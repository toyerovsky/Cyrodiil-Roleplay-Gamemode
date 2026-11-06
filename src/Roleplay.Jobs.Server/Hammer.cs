namespace Roleplay.Jobs.Server;

public sealed class Hammer
{
    public string Item { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; } = 1;
    // Its full durability, as the game's data has it; 0 when unknown, which counts it always as made
    public int Durability { get; set; }
}
