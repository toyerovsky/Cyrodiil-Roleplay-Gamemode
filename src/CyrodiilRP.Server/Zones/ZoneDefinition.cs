using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Zones;

internal sealed class ZoneDefinition
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Name { get; set; } = [];
    public int MinLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 1;

    [JsonConverter(typeof(JsonStringEnumConverter<ZoneMode>))]
    public ZoneMode Mode { get; set; } = ZoneMode.Fixed;

    public List<int[][]> Shapes { get; set; } = [];
    public List<string> Areas { get; set; } = [];

    // Interior cells by form id in hex (e.g. "00096AC5"), for dungeons that have no Tamriel grid.
    public List<string> Interiors { get; set; } = [];
    public int[] Origin { get; set; } = [0, 0];
    public double Radius { get; set; } = 1;
    private double? _npcHealth;
    private double? _npcDamage;
    public double NpcHealth { get => _npcHealth ?? BandStrength; set => _npcHealth = value; }
    public double NpcDamage { get => _npcDamage ?? BandStrength; set => _npcDamage = value; }
    private double BandStrength => MinLevel >= 35 ? 1.6 : MinLevel >= 27 ? 1.4 : MinLevel >= 21 ? 1.25 : MinLevel >= 15 ? 1.1 : 1;

    public string Named(string locale) => Name.TryGetValue(locale, out var name) ? name : Name[Localization.Localizer.BaseLocale];
}
