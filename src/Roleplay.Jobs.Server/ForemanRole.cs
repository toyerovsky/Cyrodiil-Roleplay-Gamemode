namespace Roleplay.Jobs.Server;

/// <summary>A kind of foreman: what they are called over their head and in their menu, and what they deal in.</summary>
public sealed class ForemanRole
{
    public string Name { get; set; } = "";

    // Pays the dock wages owed to the player
    public bool PaysWages { get; set; }

    public List<ForemanWare> Sells { get; set; } = [];
    public List<ForemanWare> Buys { get; set; } = [];
}
