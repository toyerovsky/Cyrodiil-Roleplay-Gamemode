using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Server;

internal sealed class LoreSettings
{
    public LoreCatalogMode CatalogMode { get; set; } = LoreCatalogMode.Canon;

    // Also offer kinds that do not fit the one body the game gives a race, such as the Senche.
    public bool AllowImplausibleSubtypes { get; set; }
}
