namespace Roleplay.Jobs.Server;

/// <summary>
/// An item a foreman sells or buys: a form id or "custom:" and an item key, its name, its price in gold, and the game's
/// editor id whose icon the window shows for it (empty for none).
/// </summary>
public sealed class ForemanWare
{
    public string Item { get; set; } = "";
    public string Name { get; set; } = "";
    public int Price { get; set; }
    public string Icon { get; set; } = "";
}
