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

    public string Named(string locale) => Name.TryGetValue(locale, out var name) ? name : Name[Localization.Localizer.BaseLocale];
}
