using System.Globalization;
using System.Text.Json;

namespace CyrodiilRP.Server.Traders;

// A ware a shop names that the game lacks, or one it values at nothing or too dearly without a price of its own, is a
// mistake in the files, so loading throws.
internal sealed class WareCatalog
{
    private const string WaresResource = "wares.json";
    private const string StocksResource = "stocks.json";

    private readonly Dictionary<string, TraderShop> _shops;

    private WareCatalog(Dictionary<string, TraderShop> shops) => _shops = shops;

    public static WareCatalog Shipped { get; } = Load(ReadResource(WaresResource), ReadResource(StocksResource));

    public IReadOnlyCollection<TraderShop> Shops => _shops.Values;

    public TraderShop? FindShop(string name) => _shops.GetValueOrDefault(name);

    public static WareCatalog Load(string waresJson, string stocksJson)
    {
        var wares = (JsonSerializer.Deserialize(waresJson, WareJson.Default.ListWareEntry) ?? [])
            .ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var stocks = JsonSerializer.Deserialize(stocksJson, WareJson.Default.StockFile)
                     ?? throw new InvalidOperationException("stocks.json is empty.");

        var shops = new Dictionary<string, TraderShop>(StringComparer.Ordinal);
        foreach (var (shop, categories) in stocks.Shops)
        {
            var list = new List<Ware>();
            foreach (var (category, ids) in categories)
            {
                foreach (var id in ids)
                {
                    if (!wares.TryGetValue(id, out var entry))
                        throw new InvalidOperationException($"Shop {shop} sells {id}, which wares.json lacks.");

                    var form = uint.Parse(entry.Form, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    list.Add(new Ware(id, form, category, ReadPrice(shop, entry, stocks.Prices), entry.Weight));
                }
            }

            shops[shop] = new TraderShop(shop, list);
        }

        var unsold = stocks.Prices.Keys.Where(id => !shops.Values.Any(shop => shop.Find(id) is not null)).ToList();
        if (unsold.Count > 0)
            throw new InvalidOperationException($"stocks.json prices {string.Join(", ", unsold)}, which no shop sells.");

        return new WareCatalog(shops);
    }

    private static int ReadPrice(string shop, WareEntry entry, Dictionary<string, int> prices)
    {
        if (prices.TryGetValue(entry.Id, out var price))
        {
            return price > 0
                ? price
                : throw new InvalidOperationException($"stocks.json prices {entry.Id} at {price}, not above 0.");
        }

        if (entry.Value is <= 0 or > TraderRules.MostValue)
            throw new InvalidOperationException($"Shop {shop} sells {entry.Id} at the game's value {entry.Value}, outside 1 to {TraderRules.MostValue}.");

        return TraderRules.ComputePrice(entry.Value);
    }

    private static string ReadResource(string resource)
    {
        using var stream = typeof(WareCatalog).Assembly.GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"The server was built without {resource}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
