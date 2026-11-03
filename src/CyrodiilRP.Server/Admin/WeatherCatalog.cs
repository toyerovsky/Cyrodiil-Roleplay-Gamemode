using System.Globalization;
using System.Text.Json;

namespace CyrodiilRP.Server.Admin;

// The plain weathers of the outside world (weathers.json), in the order the panel shows.
internal sealed class WeatherCatalog(IReadOnlyDictionary<string, uint> forms, IReadOnlyList<string> names)
{
    public static WeatherCatalog Shipped { get; } = Load(EmbeddedData.Read(EmbeddedData.Weathers));

    public IReadOnlyList<string> Names { get; } = names;

    public static WeatherCatalog Load(string json)
    {
        var entries = JsonSerializer.Deserialize(json, AdminDataJson.Default.ListWeatherEntry) ?? [];
        var forms = entries.ToDictionary(entry => entry.Id, entry => uint.Parse(entry.Form, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            StringComparer.OrdinalIgnoreCase);
        return new WeatherCatalog(forms, entries.Select(entry => entry.Id).ToList());
    }

    public bool TryFind(string name, out uint form) => forms.TryGetValue(name.Trim(), out form);
}
