using System.Text.Json.Serialization;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Server;

internal sealed class LoreSettings
{
    // By name in the config: the server reads an enum as a number unless its property names a converter.
    [JsonConverter(typeof(JsonStringEnumConverter<LoreCatalogMode>))]
    public LoreCatalogMode CatalogMode { get; set; } = LoreCatalogMode.Canon;

    // Also offer kinds that do not fit the one body the game gives a race, such as the Senche.
    public bool AllowImplausibleSubtypes { get; set; }

    // A wrong value stops the mod at start.
    public void RequireValid()
    {
        if (!Enum.IsDefined(CatalogMode))
            throw new InvalidOperationException($"server/config.json: lore.catalogMode is {CatalogMode}, which is no catalog mode.");
    }
}
