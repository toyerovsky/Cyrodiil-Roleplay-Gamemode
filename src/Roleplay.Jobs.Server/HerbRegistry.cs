using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

/// <summary>The herbs of <see cref="HerbalismConfig.HerbFile"/>, by their plant's base form id, read once at start.</summary>
[Service]
public sealed partial class HerbRegistry(JobsConfig config, ILogger logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        // The relay is published AOT with reflection JSON off; naming a resolver opts this file back in.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private Dictionary<uint, Herb> _byPlant = [];

    public IReadOnlyCollection<Herb> All => _byPlant.Values;

    public bool TryFind(uint plant, out Herb herb) => _byPlant.TryGetValue(plant, out herb!);

    /// <summary>The server mod folder, read at once. Set by the entry point before any service starts.</summary>
    public void UseFolder(string folder)
    {
        var path = Path.Combine(folder, config.Herbalism.HerbFile);
        if (!File.Exists(path))
        {
            logger.LogError("Jobs: no herb list at {Path}; herbalism gates no plant.", path);
            return;
        }

        Load(File.ReadAllText(path), path);
    }

    // Internal so the tests can load a list without the SDK starting the service
    internal void Load(string json, string source)
    {
        var herbs = JsonSerializer.Deserialize<List<Herb>>(json, Json)
            ?? throw new JsonException($"Jobs: the herb list {source} is empty.");
        _byPlant = [];
        foreach (var herb in herbs)
        {
            var plant = JobItemResolver.ParseFormId(herb.Plant);
            JobItemResolver.ParseFormId(herb.Ingredient);
            if (!Enum.IsDefined(herb.Tier))
                throw new JsonException($"Jobs: the herb {herb.Plant} in {source} has no tier the game knows ({herb.Tier}).");
            if (!_byPlant.TryAdd(plant, herb))
                throw new JsonException($"Jobs: the plant {herb.Plant} is listed twice in {source}.");
        }

        logger.LogInformation("Jobs: {Count} herbs loaded from {Source}.", _byPlant.Count, source);
    }
}
