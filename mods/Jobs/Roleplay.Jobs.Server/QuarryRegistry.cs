using System.Numerics;
using Microsoft.Extensions.Logging;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

/// <summary>A quarry: rock within <paramref name="Radius"/> centimeters of <paramref name="Center"/> can be quarried.</summary>
public sealed record QuarryArea(string Name, Vector3 Center, float Radius)
{
    public bool Holds(Vector3 point) => Vector3.Distance(point, Center) <= Radius;
}

/// <summary>
/// The quarries on this server: the config's, and with <see cref="MiningConfig.QuarriesFromMarkers"/> one around every
/// map marker whose name holds <see cref="MiningConfig.QuarryMarkerName"/> in the plugins the server hands its players
/// (<see cref="MiningConfig.PluginFolders"/>), in the worldspaces <see cref="MiningConfig.QuarryWorldspaces"/> lists.
/// Read once, when the entry point gives the server mod folder.
/// </summary>
[Service]
public sealed partial class QuarryRegistry(JobsConfig config, ILogger logger)
{
    private readonly List<QuarryArea> _quarries = [];

    public IReadOnlyList<QuarryArea> Quarries => _quarries;

    private MiningConfig Settings => config.Mining;

    /// <summary>The server mod folder, which <see cref="MiningConfig.PluginFolders"/> are relative to.</summary>
    public void UseFolder(string folder)
    {
        _quarries.Clear();
        foreach (var quarry in Settings.Quarries)
            _quarries.Add(new QuarryArea(quarry.Name, new Vector3(quarry.X, quarry.Y, quarry.Z), quarry.Radius > 0f ? quarry.Radius : Settings.QuarryRadius));

        if (Settings.QuarriesFromMarkers)
            _quarries.AddRange(PluginsIn(folder).SelectMany(FromPlugin));

        foreach (var quarry in _quarries)
            logger.LogInformation("Jobs: quarry {Name}, {Radius:0} cm around {Center}.", quarry.Name, quarry.Radius, quarry.Center);
    }

    // Every plugin under the folders, the first one found of each name
    private IEnumerable<string> PluginsIn(string folder)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relative in Settings.PluginFolders)
        {
            var pluginFolder = Path.GetFullPath(Path.Combine(folder, relative));
            if (!Directory.Exists(pluginFolder))
            {
                logger.LogInformation("Jobs: no plugin folder at {Folder}.", pluginFolder);
                continue;
            }

            var plugins = Directory.EnumerateFiles(pluginFolder, "*.esp", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(pluginFolder, "*.esm", SearchOption.AllDirectories))
                .Order(StringComparer.OrdinalIgnoreCase);
            foreach (var plugin in plugins)
            {
                if (seen.Add(Path.GetFileName(plugin)))
                    yield return plugin;
            }
        }
    }

    private IEnumerable<QuarryArea> FromPlugin(string plugin)
    {
        IReadOnlyList<PluginMapMarker> markers;
        try
        {
            markers = PluginMapMarkers.Read(File.ReadAllBytes(plugin));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            logger.LogWarning(exception, "Jobs: could not read the map markers of {Plugin}; it gives no quarries.", plugin);
            yield break;
        }

        logger.LogInformation("Jobs: {Plugin} has {Count} named map markers.", plugin, markers.Count);
        foreach (var marker in markers)
        {
            if (!marker.Name.Contains(Settings.QuarryMarkerName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Settings.QuarryWorldspaces.Contains(marker.Worldspace, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogInformation("Jobs: map marker {Name} in {Plugin} is in worldspace '{Worldspace}', which quarryWorldspaces does not list. Skipped.",
                    marker.Name, Path.GetFileName(plugin), marker.Worldspace);
                continue;
            }
            yield return new QuarryArea(marker.Name, TesUnrealSpace.ToUnrealLocation(marker.Position), Settings.QuarryRadius);
        }
    }
}
